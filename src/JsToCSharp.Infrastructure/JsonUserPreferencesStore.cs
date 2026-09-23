using System.Text.Json;
using JsToCSharp.Application;
using JsToCSharp.Domain;
namespace JsToCSharp.Infrastructure;

public sealed class JsonUserPreferencesStore(string path) : IUserPreferencesStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C2X86", "preferences.json");

    public UserPreferences Load()
    {
        try
        {
            return (JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path)) ?? new()).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UserPreferences();
        }
    }

    public void Save(UserPreferences preferences)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences.Normalize(), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
