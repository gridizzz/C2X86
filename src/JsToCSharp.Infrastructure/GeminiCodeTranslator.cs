using System.Text.Json;
using System.Net.Http.Json;
using JsToCSharp.Application;
using JsToCSharp.Domain;

namespace JsToCSharp.Infrastructure;

public sealed class GeminiCodeTranslator(HttpClient httpClient) : ICodeTranslator
{
    public AiProvider Provider => AiProvider.Gemini;

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken, IProgress<TranslationStage>? progress = null)
    {
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(request.Model)}:generateContent?key={Uri.EscapeDataString(request.ApiKey)}";
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = TranslationPrompt.Create(request) } } } },
                generationConfig = new { temperature = 0.1 }
            })
        };

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadError(responseText, "Gemini request failed."));
        }

        using var document = JsonDocument.Parse(responseText);
        var code = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Gemini returned no translated code.");
        }

        return new TranslationResult(TranslationOutput.RemoveMarkdownFence(code));
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
