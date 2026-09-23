namespace JsToCSharp.Application;

public interface ILocalModelRuntime : IAsyncDisposable
{
    string? LoadedModelPath { get; }
    Task UnloadAsync();
}
