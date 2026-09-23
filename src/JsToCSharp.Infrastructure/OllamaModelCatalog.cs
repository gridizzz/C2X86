using System.Text.Json;
using JsToCSharp.Application;
namespace JsToCSharp.Infrastructure;

public sealed class OllamaModelCatalog : ILocalModelCatalog
{
    public Task<string> GetDisplayNameAsync(string path, CancellationToken cancellationToken) => Task.Run(() =>
    {
        if (string.IsNullOrWhiteSpace(path)) return "No local model selected";
        var filename = Path.GetFileName(path);
        var fallback = filename.StartsWith("sha256-", StringComparison.Ordinal) ? "Ollama model" : Path.GetFileNameWithoutExtension(path);
        try
        {
            var file = new FileInfo(path);
            if (file.Directory?.Name != "blobs" || !filename.StartsWith("sha256-", StringComparison.Ordinal)) return fallback;
            var manifests = Path.Combine(file.Directory.Parent!.FullName, "manifests");
            if (!Directory.Exists(manifests)) return fallback;
            var digest = "sha256:" + filename[7..];
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var manifest in Directory.EnumerateFiles(manifests, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (new FileInfo(manifest).Length > 1024 * 1024) continue;
                    using var json = JsonDocument.Parse(File.ReadAllText(manifest));
                    if (!json.RootElement.TryGetProperty("layers", out var layers) || layers.ValueKind != JsonValueKind.Array) continue;
                    foreach (var layer in layers.EnumerateArray())
                    {
                        if (layer.ValueKind != JsonValueKind.Object ||
                            !layer.TryGetProperty("mediaType", out var media) || media.GetString() != "application/vnd.ollama.image.model" ||
                            !layer.TryGetProperty("digest", out var value) || value.GetString() != digest) continue;
                        var model = Directory.GetParent(manifest)!.Name;
                        var tag = Path.GetFileName(manifest);
                        names.Add(char.ToUpperInvariant(model[0]) + model[1..] + " " + tag.Replace("b", "B", StringComparison.Ordinal));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
            }
            return names.Count > 0 ? string.Join(" / ", names) : fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return fallback;
        }
    }, cancellationToken);
}
