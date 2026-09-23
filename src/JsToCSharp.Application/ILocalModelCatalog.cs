namespace JsToCSharp.Application;

public interface ILocalModelCatalog
{
    Task<string> GetDisplayNameAsync(string path, CancellationToken cancellationToken);
}
