using System.Text.Json;
using JsToCSharp.Application;
using JsToCSharp.Domain;
namespace JsToCSharp.Infrastructure;

public sealed class JsonDraftStore(string path) : IDraftStore
{
    public RecoveryDraft? Load()
    {
        try
        {
            var draft = JsonSerializer.Deserialize<RecoveryDraft>(File.ReadAllText(path));
            return draft is not null && draft.SourceCode is not null && draft.ResultCode is not null &&
                Enum.IsDefined(draft.SourceLanguage) && Enum.IsDefined(draft.TargetLanguage) ? draft : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public void Save(RecoveryDraft draft)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, draft);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Delete()
    {
        File.Delete(path);
        File.Delete(Path.GetFullPath(path) + ".tmp");
    }
}
