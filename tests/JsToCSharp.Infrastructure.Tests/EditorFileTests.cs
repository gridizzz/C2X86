using System.Text;
using JsToCSharp.Domain;
using JsToCSharp.Infrastructure;
using Xunit;
namespace JsToCSharp.Infrastructure.Tests;

public sealed class EditorFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "c2x86-editor-tests-" + Guid.NewGuid().ToString("N"));
    public EditorFileTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void DraftRoundTripReplacementAndDeletion()
    {
        var path = Path.Combine(_directory, "draft.json");
        var store = new JsonDraftStore(path);
        Assert.Null(store.Load());
        var draft = new RecoveryDraft("let café = '☕';\n", "// result", Language.JavaScript, Language.CSharp);
        store.Save(draft);
        Assert.Equal(draft, store.Load());
        var next = draft with { SourceCode = "updated" };
        store.Save(next);
        Assert.Equal(next, store.Load());
        Assert.Single(Directory.GetFiles(_directory));
        File.WriteAllText(path + ".tmp", "interrupted write");
        Assert.Equal(next, store.Load());
        store.Delete();
        Assert.Null(store.Load());
        Assert.Empty(Directory.GetFiles(_directory));
        store.Delete();
    }

    [Fact]
    public void FailedDraftWritePreservesPreviousSnapshot()
    {
        var path = Path.Combine(_directory, "draft.json");
        var store = new JsonDraftStore(path);
        var original = new RecoveryDraft("original", "result", Language.JavaScript, Language.CSharp);
        store.Save(original);
        Directory.CreateDirectory(path + ".tmp");
        Assert.ThrowsAny<Exception>(() => store.Save(original with { SourceCode = "new" }));
        Assert.Equal(original, store.Load());
    }

    [Theory]
    [InlineData("{unfinished")]
    [InlineData("{}")]
    [InlineData("{\"SourceCode\":\"a\",\"ResultCode\":\"b\",\"SourceLanguage\":999}")]
    public void InvalidDraftIsNotRestored(string contents)
    {
        var path = Path.Combine(_directory, "draft.json");
        File.WriteAllText(path, contents);
        Assert.Null(new JsonDraftStore(path).Load());
    }

    [Fact]
    public void RecoveryIsOptInAndPreferenceContainsNoDraft()
    {
        var path = Path.Combine(_directory, "prefs.json");
        var store = new JsonUserPreferencesStore(path);
        Assert.False(store.Load().DraftRecoveryEnabled);
        store.Save(new UserPreferences { DraftRecoveryEnabled = true });
        Assert.True(store.Load().DraftRecoveryEnabled);
        Assert.False(File.Exists(Path.Combine(_directory, "draft.json")));
    }

    [Theory]
    [InlineData("sample.js")]
    [InlineData("sample.CS")]
    [InlineData("sample.txt")]
    public async Task ReadsSourceTextWithoutExecutingIt(string name)
    {
        const string text = "throw new Error('not executed');\n// café";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        Assert.Equal(text, await new InputFileService().ReadSourceAsync(stream, name));
    }

    [Theory]
    [InlineData("model.gguf", "GGUF")]
    [InlineData("binary.js", "abc\0def")]
    public async Task RejectsUnsupportedOrBinaryFile(string name, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new InputFileService().ReadSourceAsync(stream, name));
    }

    [Fact]
    public async Task RejectsOversizedSource()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 2 * 1024 * 1024 + 1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new InputFileService().ReadSourceAsync(stream, "large.js"));
    }

    [Fact]
    public void ModelDropValidationChecksHeaderNotExtension()
    {
        var path = Path.Combine(_directory, "sha256-example");
        File.WriteAllText(path, "GGUF");
        new InputFileService().ValidateModel(path);
        File.WriteAllText(path, "{}");
        Assert.Throws<InvalidOperationException>(() => new InputFileService().ValidateModel(path));
    }
}
