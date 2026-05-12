namespace LibraryCompiler.Services;

/// <summary>
/// Read/write key→bytes storage that the host injects when it wants the
/// compiler to persist artifacts across sessions. The compiler treats
/// this strictly as an opaque blob store — it never asks where the bytes
/// live, what filesystem is used, what quota applies, or how eviction is
/// handled. Those are host concerns.
/// </summary>
/// <remarks>
/// <para>
/// Reference implementations live in the host (<c>Kmila.Shared/Services/</c>):
/// <list type="bullet">
///   <item><c>FileSystemBlobStore(rootPath)</c> for desktop / dev tree.</item>
///   <item><c>MauiAppDataBlobStore</c> for mobile (wraps <c>FileSystem.AppDataDirectory</c>).</item>
///   <item><c>IsolatedStorageBlobStore</c> for sandboxed environments.</item>
/// </list>
/// The lib ships <see cref="InMemoryBlobStore"/> and <see cref="NullBlobStore"/>
/// as portable defaults — useful for tests and for hosts that don't want
/// any persistence at all.
/// </para>
/// <para>
/// Keys are caller-defined strings; the compiler uses content-hash keys
/// for IEEE / user libraries so a different source produces a different
/// key automatically (no mtime-based invalidation needed).
/// </para>
/// </remarks>
public interface IBlobStore
{
    /// <summary>True iff <paramref name="key"/> has a stored value.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Returns the bytes for <paramref name="key"/>, or <c>null</c> when missing.</summary>
    Task<byte[]?> ReadAsync(string key, CancellationToken ct = default);

    /// <summary>Writes <paramref name="payload"/> under <paramref name="key"/>, replacing any prior value.</summary>
    Task WriteAsync(string key, byte[] payload, CancellationToken ct = default);

    /// <summary>Removes <paramref name="key"/> if present. No-op when missing.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Streams every stored key. The host can use this to compute total
    /// usage for LRU eviction policy decisions. Yielding lazily so a
    /// large store doesn't materialise the full list in memory.
    /// </summary>
    IAsyncEnumerable<string> EnumerateKeysAsync(CancellationToken ct = default);
}
