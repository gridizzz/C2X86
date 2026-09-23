using JsToCSharp.Application;
using JsToCSharp.Domain;
using Xunit;

namespace JsToCSharp.Application.Tests;

public sealed class TranslationServiceTests
{
    private static TranslationRequest Request(AiProvider provider, string? path = null) =>
        new(provider, "", "", Language.JavaScript, Language.CSharp, "let x = 1;", path);

    [Fact]
    public async Task LocalRequestNeedsNoApiKeyOrModelId()
    {
        var service = new TranslationService([new FakeTranslator()]);
        Assert.Equal("translated", (await service.TranslateAsync(Request(AiProvider.LocalModel, "model"), default)).Code);
    }

    [Fact]
    public async Task LocalRequestRequiresPath()
    {
        var service = new TranslationService([new FakeTranslator()]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.TranslateAsync(Request(AiProvider.LocalModel), default));
    }

    [Theory]
    [InlineData(AiProvider.OpenAi)]
    [InlineData(AiProvider.Gemini)]
    public async Task CloudRequestsStillRequireCredentials(AiProvider provider)
    {
        var service = new TranslationService([]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.TranslateAsync(Request(provider), default));
    }

    [Fact]
    public async Task CancellationStopsBeforeDispatch()
    {
        var service = new TranslationService([new FakeTranslator()]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.TranslateAsync(Request(AiProvider.LocalModel, "model"), new CancellationToken(true)));
    }

    private sealed class FakeTranslator : ICodeTranslator
    {
        public AiProvider Provider => AiProvider.LocalModel;
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken, IProgress<TranslationStage>? progress = null)
            => Task.FromResult(new TranslationResult("translated"));
    }
}
