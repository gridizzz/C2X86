using JsToCSharp.Domain;
namespace JsToCSharp.Application;

public interface IDraftStore
{
    RecoveryDraft? Load();
    void Save(RecoveryDraft draft);
    void Delete();
}
