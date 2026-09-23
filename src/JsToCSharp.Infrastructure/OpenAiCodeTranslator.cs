using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JsToCSharp.Application;
using JsToCSharp.Domain;

namespace JsToCSharp.Infrastructure;

public sealed class OpenAiCodeTranslator(HttpClient httpClient) : ICodeTranslator
{
    public AiProvider Provider => AiProvider.OpenAi;

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken, IProgress<TranslationStage>? progress = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        message.Content = JsonContent.Create(new
        {
            model = request.Model,
            input = TranslationPrompt.Create(request)
        });

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadError(responseText, "OpenAI request failed."));
        }

        using var document = JsonDocument.Parse(responseText);
        var code = ReadOutputText(document.RootElement);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("OpenAI returned no translated code.");
        }

        return new TranslationResult(TranslationOutput.RemoveMarkdownFence(code));
    }

    private static string ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output)) return string.Empty;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content)) continue;
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static string ReadError(string json, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("error").GetProperty("message").GetString() ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

}
