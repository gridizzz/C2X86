using JsToCSharp.Domain;
using JsToCSharp.Infrastructure;
using Xunit;

namespace JsToCSharp.Infrastructure.Tests;

public sealed class RecoveryFeatureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "c2x86-tests-" + Guid.NewGuid().ToString("N"));
    public RecoveryFeatureTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void PreferencesRoundTripAndExcludeSecretsAndCode()
    {
        var path = Path.Combine(_directory, "preferences.json");
        var store = new JsonUserPreferencesStore(path);
        var expected = new UserPreferences { Provider = AiProvider.LocalModel, LocalModelPath = "/models/qwen.gguf", WordWrap = true, EditorFontSize = 18, Maximized = true };
        store.Save(expected);
        Assert.Equal(expected, store.Load());
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(new[] { "DraftRecoveryEnabled", "EditorFontSize", "LocalModelPath", "Maximized", "Provider", "SourceLanguage", "TargetLanguage", "WindowHeight", "WindowWidth", "WordWrap" },
            json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void MissingOrBrokenPreferencesUseDefaultsAndValuesAreBounded()
    {
        var path = Path.Combine(_directory, "preferences.json");
        var store = new JsonUserPreferencesStore(path);
        Assert.Equal(new UserPreferences(), store.Load());
        File.WriteAllText(path, "{unfinished");
        Assert.Equal(new UserPreferences(), store.Load());
        File.WriteAllText(path, "{\"Provider\":999,\"EditorFontSize\":100,\"WindowWidth\":1}");
        Assert.Equal(AiProvider.OpenAi, store.Load().Provider);
        Assert.Equal(32, store.Load().EditorFontSize);
        Assert.Equal(900, store.Load().WindowWidth);
    }

    [Fact]
    public async Task CatalogResolvesModelLayerAndIgnoresMalformedManifests()
    {
        var blobs = Directory.CreateDirectory(Path.Combine(_directory, "blobs")).FullName;
        var manifests = Directory.CreateDirectory(Path.Combine(_directory, "manifests", "registry.ollama.ai", "library", "qwen3")).FullName;
        File.WriteAllText(Path.Combine(manifests, "broken"), "[]");
        File.WriteAllText(Path.Combine(manifests, "0.6b"), "{\"layers\":[{\"mediaType\":\"application/vnd.ollama.image.model\",\"digest\":\"sha256:abc\"}]}");
        var catalog = new OllamaModelCatalog();
        Assert.Equal("Qwen3 0.6B", await catalog.GetDisplayNameAsync(Path.Combine(blobs, "sha256-abc"), default));
        Assert.Equal("Ollama model", await catalog.GetDisplayNameAsync(Path.Combine(blobs, "sha256-unknown"), default));
        Assert.Equal("custom", await catalog.GetDisplayNameAsync(Path.Combine(_directory, "custom.gguf"), default));
    }

    [Fact]
    public async Task CacheReusesWeightsReplacesAndUnloadsThem()
    {
        await using var cache = new ModelCache<Resource>();
        var first = new Resource();
        var second = new Resource();
        await cache.UseAsync("first", _ => Task.FromResult(first), value => Task.FromResult(value), default);
        var reused = await cache.UseAsync("first", _ => throw new Exception("Must reuse"), value => Task.FromResult(value), default);
        Assert.Same(first, reused);
        await cache.UseAsync("second", _ => Task.FromResult(second), value => Task.FromResult(value), default);
        Assert.True(first.Disposed);
        Assert.False(second.Disposed);
        await cache.UnloadAsync();
        Assert.True(second.Disposed);
        Assert.Null(cache.LoadedKey);
    }

    [Fact]
    public async Task CacheWaitsForInferenceBeforeDisposal()
    {
        var cache = new ModelCache<Resource>();
        var resource = new Resource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inference = cache.UseAsync("model", _ => Task.FromResult(resource), async value =>
        {
            started.SetResult();
            await finish.Task;
            Assert.False(value.Disposed);
            return true;
        }, default);
        await started.Task;
        var disposal = cache.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        finish.SetResult();
        await inference;
        await disposal;
        Assert.True(resource.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.UseAsync("model", _ => Task.FromResult(new Resource()), _ => Task.FromResult(true), default));
    }

    [Fact]
    public async Task CancelledLoadingDisposesNewWeights()
    {
        await using var cache = new ModelCache<Resource>();
        using var cancellation = new CancellationTokenSource();
        var resource = new Resource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.UseAsync("model", _ =>
        {
            cancellation.Cancel();
            return Task.FromResult(resource);
        }, _ => Task.FromResult(true), cancellation.Token));
        Assert.True(resource.Disposed);
        Assert.Null(cache.LoadedKey);
    }

    private sealed class Resource : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
