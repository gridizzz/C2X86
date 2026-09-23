using JsToCSharp.Domain;
namespace JsToCSharp.Application;

public interface IUserPreferencesStore
{
    UserPreferences Load();
    void Save(UserPreferences preferences);
}
