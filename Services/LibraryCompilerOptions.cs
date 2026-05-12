using LibraryCompiler.Repositories;

namespace LibraryCompiler.Services;

/// <summary>
/// Everything the host injects when constructing a <see cref="LibraryCompiler"/>.
/// All path/platform decisions live in this record — the lib itself
/// touches no filesystem, no environment variables, no special folders.
/// </summary>
/// <param name="OpenLibrarySource">
/// Resolves a <see cref="LibrarySource.SourceKey"/> to a readable byte
/// stream. The host decides what the key means (relative path, embedded
/// resource name, absolute path, …); the compiler treats it as opaque.
/// Returning <c>null</c> signals the host can't provide source for that
/// key — the compiler falls back to its built-in fallback list when the
/// missing library is one of the IEEE builtins.
/// </param>
/// <param name="CacheStore">
/// Optional persistence. Pass <see cref="NullBlobStore.Instance"/> (or
/// leave null and the compiler will substitute it) to disable disk
/// persistence. Used only when <see cref="Policy"/> requests
/// <see cref="LibraryBuildMode.Persistent"/>.
/// </param>
/// <param name="AdditionalSources">
/// User-imported libraries (plugins) the host has discovered. The
/// compiler merges these with its IEEE builtins so user code can
/// <c>use mylib.something.all</c> through the same machinery.
/// </param>
/// <param name="Policy">
/// Build mode (and per-library overrides). Defaults to
/// <see cref="LibraryBuildPolicy.AlwaysProcessCached"/> if the host
/// doesn't supply one — but the host SHOULD make this decision based
/// on the target device's resources rather than relying on the default.
/// </param>
public sealed record LibraryCompilerOptions(
    Func<string, CancellationToken, Task<Stream?>> OpenLibrarySource,
    IBlobStore? CacheStore = null,
    IReadOnlyList<LibrarySource>? AdditionalSources = null,
    LibraryBuildPolicy? Policy = null);
