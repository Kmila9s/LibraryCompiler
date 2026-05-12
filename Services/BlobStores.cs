using System.Runtime.CompilerServices;

namespace LibraryCompiler.Services;

/// <summary>
/// In-process dictionary-backed blob store. Useful for tests and for hosts
/// that want session-scoped persistence without touching disk.
/// </summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    /// <summary>Underlying key → bytes map. All access guarded by <see cref="_lock"/>.</summary>
    private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
    /// <summary>Mutex guarding <see cref="_store"/>.</summary>
    private readonly object _lock = new();

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_store.ContainsKey(key));
    }

    /// <inheritdoc/>
    public Task<byte[]?> ReadAsync(string key, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);
        }
    }

    /// <inheritdoc/>
    public Task WriteAsync(string key, byte[] payload, CancellationToken ct = default)
    {
        lock (_lock) _store[key] = payload;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        lock (_lock) _store.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> EnumerateKeysAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string[] snapshot;
        lock (_lock) snapshot = _store.Keys.ToArray();
        foreach (var k in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return k;
            await Task.Yield();
        }
    }
}

/// <summary>
/// No-op blob store. Use as the cache when persistence is disabled —
/// every read returns <c>null</c>, every write is silently dropped. The
/// compiler treats this as equivalent to "no cache available" and
/// behaves under the OnDemand or ProcessCached policies accordingly.
/// </summary>
public sealed class NullBlobStore : IBlobStore
{
    /// <summary>Singleton instance (the store is stateless).</summary>
    public static readonly NullBlobStore Instance = new();

    /// <summary>Private constructor — use <see cref="Instance"/>.</summary>
    private NullBlobStore() { }

    /// <inheritdoc/>
    public Task<bool>    ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
    /// <inheritdoc/>
    public Task<byte[]?> ReadAsync  (string key, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
    /// <inheritdoc/>
    public Task          WriteAsync (string key, byte[] payload, CancellationToken ct = default) => Task.CompletedTask;
    /// <inheritdoc/>
    public Task          DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    /// <inheritdoc/>
    public async IAsyncEnumerable<string> EnumerateKeysAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
