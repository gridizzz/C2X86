namespace JsToCSharp.Application;

public interface IInputFileService
{
    Task<string> ReadSourceAsync(Stream stream, string name);
    void ValidateModel(string path);
}
