using JsToCSharp.Application;
using JsToCSharp.Domain;
using Xunit;

namespace JsToCSharp.Application.Tests;

public sealed class LanguageSupportTests
{
    public static IEnumerable<object[]> Routes =>
        from provider in Enum.GetValues<AiProvider>()
        from source in Enum.GetValues<Language>()
        from target in Enum.GetValues<Language>()
        where source != target
        select new object[] { provider, source, target };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task RoutesEveryDistinctPairWithoutChangingInput(AiProvider provider, Language source, Language target)
    {
        var translator = new RecordingTranslator(provider);
        var request = new TranslationRequest(provider, "test-model", "test-key", source, target,
            "    significant indentation\n\n", "test.gguf");
        var result = await new TranslationService([translator]).TranslateAsync(request, default);
        Assert.Same(request, translator.Request);
        Assert.Equal("result", result.Code);
    }

    [Theory]
    [InlineData(Language.Python, Language.Python)]
    [InlineData(Language.Cpp, Language.Cpp)]
    [InlineData((Language)999, Language.Java)]
    [InlineData(Language.Go, (Language)(-1))]
    public async Task RejectsInvalidOrIdenticalLanguagesBeforeDispatch(Language source, Language target)
    {
        var translator = new RecordingTranslator(AiProvider.LocalModel);
        var request = new TranslationRequest(AiProvider.LocalModel, "", "", source, target, "source", "model.gguf");
        await Assert.ThrowsAsync<ArgumentException>(() => new TranslationService([translator]).TranslateAsync(request, default));
        Assert.Null(translator.Request);
    }

    [Theory]
    [InlineData(Language.JavaScript, "JavaScript", ".js")]
    [InlineData(Language.CSharp, "C#", ".cs")]
    [InlineData(Language.Python, "Python", ".py")]
    [InlineData(Language.TypeScript, "TypeScript", ".ts")]
    [InlineData(Language.Java, "Java", ".java")]
    [InlineData(Language.Go, "Go", ".go")]
    [InlineData(Language.Rust, "Rust", ".rs")]
    [InlineData(Language.C, "C", ".c")]
    [InlineData(Language.Cpp, "C++", ".cpp")]
    [InlineData(Language.PHP, "PHP", ".php")]
    [InlineData(Language.Kotlin, "Kotlin", ".kt")]
    [InlineData(Language.Swift, "Swift", ".swift")]
    [InlineData(Language.Ruby, "Ruby", ".rb")]
    public void ExportMetadataMatchesLanguage(Language language, string name, string extension)
    {
        var info = LanguageCatalog.Get(language);
        Assert.Equal(name, info.DisplayName);
        Assert.Equal(extension, info.FileExtension);
        Assert.EndsWith(extension, info.SuggestedFileName);
        Assert.Contains(extension, LanguageCatalog.SourceExtensions);
    }

    [Fact]
    public void CatalogCoversAllLanguagesAndRetainsPersistedIds()
    {
        Assert.Equal(0, (int)Language.JavaScript);
        Assert.Equal(1, (int)Language.CSharp);
        Assert.Equal(8, (int)Language.Cpp);
        Assert.Equal(9, (int)Language.PHP);
        Assert.Equal(10, (int)Language.Kotlin);
        Assert.Equal(11, (int)Language.Swift);
        Assert.Equal(12, (int)Language.Ruby);
        Assert.Equal(Enum.GetValues<Language>(), LanguageCatalog.All.Select(info => info.Language));
        Assert.Throws<ArgumentOutOfRangeException>(() => LanguageCatalog.Get((Language)999));
    }

    private sealed class RecordingTranslator(AiProvider provider) : ICodeTranslator
    {
        public AiProvider Provider => provider;
        public TranslationRequest? Request { get; private set; }
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken,
            IProgress<TranslationStage>? progress = null)
        {
            Request = request;
            return Task.FromResult(new TranslationResult("result"));
        }
    }
}
