using System.Net;
using System.Text;
using System.Text.Json;
using JsToCSharp.Application;
using JsToCSharp.Domain;
using JsToCSharp.Infrastructure;
using Xunit;

namespace JsToCSharp.Infrastructure.Tests;

public sealed class TranslationOutputTests
{
    [Theory]
    [InlineData("python")]
    [InlineData("typescript")]
    [InlineData("java")]
    [InlineData("go")]
    [InlineData("rust")]
    [InlineData("c")]
    [InlineData("cpp")]
    [InlineData("c++")]
    [InlineData("csharp")]
    [InlineData("javascript")]
    [InlineData("")]
    public void RemovesLanguageFenceAndPreservesIndentedCodeAndLiteralBackticks(string tag)
    {
        const string code = "    text = '```literal```'\n    other = '</think>'";
        var output = $"```{tag}\n{code}\n```";
        Assert.Equal(code, TranslationOutput.RemoveMarkdownFence(output));
        Assert.Equal(code, LocalCodeTranslator.CleanOutput(output));
    }

    [Theory]
    [InlineData("    first_line\n    second_line\n")]
    [InlineData("value = '```python and </think>'\n")]
    [InlineData("\n\nconst template = `hello`;\n\n")]
    public void UnwrappedCodeIsUnchanged(string code)
    {
        Assert.Equal(code, TranslationOutput.RemoveMarkdownFence(code));
        Assert.Equal(code, LocalCodeTranslator.CleanOutput(code));
    }

    [Fact]
    public void HandlesCrLfAndLeadingReasoningWithoutRemovingIndentation()
    {
        const string code = "    return 1;";
        Assert.Equal(code, TranslationOutput.RemoveMarkdownFence("\r\n```rust\r\n" + code + "\r\n```\r\n"));
        Assert.Equal(code, LocalCodeTranslator.CleanOutput("<think>reasoning</think>\n" + code));
    }

    [Theory]
    [InlineData(AiProvider.OpenAi)]
    [InlineData(AiProvider.Gemini)]
    public async Task CloudAdaptersUseNewLanguagePromptAndSafeOutputCleanup(AiProvider provider)
    {
        const string code = "    print('```literal```')";
        var output = "```python\n" + code + "\n```";
        var payload = provider == AiProvider.OpenAi
            ? JsonSerializer.Serialize(new { output = new[] { new { content = new[] { new { type = "output_text", text = output } } } } })
            : JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = output } } } } } });
        using var handler = new ResponseHandler(payload);
        using var client = new HttpClient(handler);
        ICodeTranslator translator = provider == AiProvider.OpenAi ? new OpenAiCodeTranslator(client) : new GeminiCodeTranslator(client);
        var result = await translator.TranslateAsync(new TranslationRequest(provider, "test-model", "test-key",
            Language.Cpp, Language.Python, "int x = 1;"), default);
        Assert.Equal(code, result.Code);
        Assert.Contains("supplied C++ source into idiomatic Python", handler.Body);
    }

    private sealed class ResponseHandler(string payload) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Decode the JSON before inspecting the prompt (C++ can be escaped in JSON).
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            Body = document.RootElement.TryGetProperty("input", out var input)
                ? input.GetString()!
                : document.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }
}
