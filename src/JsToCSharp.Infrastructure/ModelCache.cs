namespace JsToCSharp.Infrastructure;

// Serializes inference, replacement, unload, and shutdown so native weights cannot
// be freed while an executor is using them. Only weights are reused, not chat history.
internal sealed class ModelCache<T> : IAsyncDisposable where T : class, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T? _value;
    private string? _key;
    private bool _disposed;
    public string? LoadedKey => Volatile.Read(ref _key);

    public async Task<TResult> UseAsync<TResult>(string key, Func<CancellationToken, Task<T>> load,
        Func<T, Task<TResult>> use, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_key != key)
            {
                Release();
                var loaded = await load(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    loaded.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                _value = loaded;
                Volatile.Write(ref _key, key);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return await use(_value!);
        }
        finally { _gate.Release(); }
    }

    public async Task UnloadAsync()
    {
        await _gate.WaitAsync();
        try { Release(); }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { _disposed = true; Release(); }
        finally { _gate.Release(); }
    }

    private void Release()
    {
        Volatile.Write(ref _key, null);
        _value?.Dispose();
        _value = null;
    }
}
