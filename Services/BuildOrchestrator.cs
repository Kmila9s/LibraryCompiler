using System.Collections.Concurrent;
using System.Text.Json;
using LibraryCompiler.Repositories;

namespace LibraryCompiler.Services;

/// <summary>
/// How the orchestrator runs builds. Linear is deterministic and the
/// default; Parallel kicks each library off on a Task.Run with bounded
/// concurrency so large multi-library projects don't blow the
/// workstation-GC budget.
/// </summary>
public enum BuildExecutionMode
{
    /// <summary>One library at a time, in topological order.</summary>
    Linear,
    /// <summary>Concurrent build, bounded by <see cref="BuildOrchestrator.MaxParallelism"/>.</summary>
    Parallel
}

/// <summary>
/// Drives the full build pass: resolve names, walk the dependency DAG,
/// dispatch each library through <see cref="LibraryBuilder"/>, honour the
/// <see cref="LibraryBuildPolicy"/>, and consult the host's
/// <see cref="IBlobStore"/> for persistence under
/// <see cref="LibraryBuildMode.Persistent"/>.
/// </summary>
public sealed class BuildOrchestrator
{
    /// <summary>Host-supplied options bundle (callbacks, additional sources, policy, cache store).</summary>
    private readonly LibraryCompilerOptions _options;
    /// <summary>Library catalog driven by name → <see cref="LibrarySource"/> lookups.</summary>
    private readonly LibraryResolver _resolver;
    /// <summary>Builder that compiles a single source into a <see cref="CompiledLibrary"/>.</summary>
    private readonly LibraryBuilder _builder;
    /// <summary>Mutable build policy, swappable at runtime via <see cref="Policy"/>.</summary>
    private LibraryBuildPolicy _policy;
    /// <summary>Persistent blob cache delegated by the host; never null (defaults to <see cref="NullBlobStore"/>).</summary>
    private readonly IBlobStore _store;

    /// <summary>
    /// The resolver instance the orchestrator drives. Exposed so the
    /// <see cref="LibraryCompiler"/> facade can hand the same resolver back
    /// to callers without needing to peek at private state through reflection.
    /// </summary>
    public LibraryResolver Resolver => _resolver;

    /// <summary>
    /// Active build policy. Mutable so the host can react to a settings
    /// change (Settings → Build mode) without rebuilding the compiler:
    /// already-cached artifacts stay valid, but subsequent compiles use
    /// the new mode for any library not already in the in-memory cache.
    /// </summary>
    public LibraryBuildPolicy Policy
    {
        get => _policy;
        set => _policy = value ?? LibraryBuildPolicy.AlwaysProcessCached;
    }

    /// <summary>Process-cached artifacts keyed by library name. Cleared via <see cref="Reset"/>.</summary>
    private readonly Dictionary<string, CompiledLibrary> _processCache =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Mutex guarding <see cref="_processCache"/> against concurrent <see cref="BuildExecutionMode.Parallel"/> builds.</summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// Default upper bound for concurrent library builds. Half the machine
    /// is a friendly default that avoids blowing the workstation-GC budget
    /// from #7. Hosts can override via the <see cref="MaxParallelism"/>
    /// property before <see cref="BuildAsync"/> is called.
    /// </summary>
    public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Constructs the orchestrator with host-supplied options.</summary>
    public BuildOrchestrator(LibraryCompilerOptions options)
    {
        _options  = options;
        _resolver = new LibraryResolver(options.AdditionalSources);
        _builder  = new LibraryBuilder(options);
        _policy   = options.Policy ?? LibraryBuildPolicy.AlwaysProcessCached;
        _store    = options.CacheStore ?? NullBlobStore.Instance;
    }

    /// <summary>
    /// Compiles every library named in <paramref name="requested"/> plus
    /// their transitive dependencies. Honours the build policy: cached
    /// artifacts skip the build entirely, Persistent reads/writes the
    /// blob store, OnDemand drops artifacts immediately after returning.
    /// </summary>
    /// <param name="requested">Library names from <c>use X.Y.all</c> clauses.</param>
    /// <param name="execution">Linear (deterministic) or Parallel (faster on many libs).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<CompiledLibrary>> BuildAsync(
        IEnumerable<string> requested,
        BuildExecutionMode execution = BuildExecutionMode.Linear,
        CancellationToken ct = default)
    {
        var ordered = _resolver.ResolveTransitive(requested);
        if (ordered.Count == 0) return Array.Empty<CompiledLibrary>();

        return execution switch
        {
            BuildExecutionMode.Linear   => await BuildLinearAsync(ordered, ct),
            BuildExecutionMode.Parallel => await BuildParallelAsync(ordered, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(execution))
        };
    }

    /// <summary>Build artifacts for the given source list one at a time.</summary>
    private async Task<IReadOnlyList<CompiledLibrary>> BuildLinearAsync(
        IReadOnlyList<LibrarySource> ordered, CancellationToken ct)
    {
        var results = new List<CompiledLibrary>(ordered.Count);
        foreach (var src in ordered)
            results.Add(await BuildOneAsync(src, ct));
        return results;
    }

    /// <summary>
    /// Concurrent build with bounded parallelism. Diagnostic ordering is
    /// stabilised by sorting the result list by (library name) on emit so
    /// the consumer doesn't see non-deterministic output.
    /// </summary>
    private async Task<IReadOnlyList<CompiledLibrary>> BuildParallelAsync(
        IReadOnlyList<LibrarySource> ordered, CancellationToken ct)
    {
        using var sem = new SemaphoreSlim(MaxParallelism);
        var bag = new ConcurrentBag<CompiledLibrary>();

        var tasks = ordered.Select(async src =>
        {
            await sem.WaitAsync(ct);
            try    { bag.Add(await BuildOneAsync(src, ct)); }
            finally{ sem.Release(); }
        });

        await Task.WhenAll(tasks);
        // Stable order by library name keeps diagnostic output deterministic.
        return bag.OrderBy(c => c.LibraryName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Build (or recall from cache) exactly one library, applying the
    /// policy that's active for it.
    /// </summary>
    /// <remarks>
    /// Persistent reads validate the cached <see cref="CompiledLibrary.ContentHash"/>
    /// against a freshly-computed hash of the live source bytes. Edits to a
    /// user plugin invalidate the blob automatically — no mtime tracking,
    /// no manual cache busting. Source bytes read for the validation are
    /// threaded into the cold-build path so we don't read them twice on a
    /// miss.
    /// </remarks>
    private async Task<CompiledLibrary> BuildOneAsync(LibrarySource src, CancellationToken ct)
    {
        var mode = _policy.ResolveFor(src.LibraryName);

        // Fast path 1: process-cached.
        if (mode != LibraryBuildMode.OnDemand)
        {
            lock (_cacheLock)
            {
                if (_processCache.TryGetValue(src.LibraryName, out var hit)) return hit;
            }
        }

        // Fast path 2: persistent blob store, with content-hash validation.
        byte[]? sourceBytes = null;
        string? sourceHash  = null;
        if (mode == LibraryBuildMode.Persistent)
        {
            sourceBytes = await _builder.ReadSourceBytesAsync(src, ct);
            sourceHash  = sourceBytes != null ? LibraryBuilder.Sha256Hex(sourceBytes) : null;

            var fromDisk = await TryReadFromBlobAsync(src, ct);
            if (fromDisk != null)
            {
                bool hashMatches = sourceHash != null &&
                                   string.Equals(fromDisk.ContentHash, sourceHash, StringComparison.OrdinalIgnoreCase);

                // Accept the blob when the source matches, OR when we have no
                // source to validate against (preserves the "trimmed deployment
                // still works" property — better stale symbols than none).
                if (hashMatches || sourceHash == null)
                {
                    StoreInProcessCache(fromDisk);
                    return fromDisk;
                }

                // Source has drifted from the cached artifact — drop the stale
                // entry now so a parallel cancelled build doesn't see it again.
                try { await _store.DeleteAsync(BlobKey(src), ct); } catch { }
            }
        }

        // Cold build — pass the bytes we already read so the builder doesn't
        // re-issue the host callback.
        var built = await _builder.BuildAsync(src, sourceBytes, ct);

        if (mode != LibraryBuildMode.OnDemand)
            StoreInProcessCache(built);

        if (mode == LibraryBuildMode.Persistent)
            await TryWriteToBlobAsync(built, ct);

        return built;
    }

    /// <summary>Inserts or overwrites the in-memory cache entry for <paramref name="lib"/> under its <see cref="CompiledLibrary.LibraryName"/>.</summary>
    private void StoreInProcessCache(CompiledLibrary lib)
    {
        lock (_cacheLock) _processCache[lib.LibraryName] = lib;
    }

    /// <summary>
    /// Empties the in-memory artifact cache. Does NOT touch the host's
    /// <see cref="IBlobStore"/> — that's a host-policy decision, not a
    /// build-orchestrator concern.
    /// </summary>
    public void Reset()
    {
        lock (_cacheLock) _processCache.Clear();
    }

    /// <summary>Read-only view of the in-memory cache (for diagnostics / tests).</summary>
    public IReadOnlyDictionary<string, CompiledLibrary> ProcessCacheSnapshot()
    {
        lock (_cacheLock) return new Dictionary<string, CompiledLibrary>(_processCache, StringComparer.OrdinalIgnoreCase);
    }

    // ---- IBlobStore (de)serialisation ------------------------------------

    /// <summary>JSON options used for blob-cache (de)serialisation. Compact, properties only.</summary>
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = false,
        IncludeFields = false
    };

    /// <summary>
    /// Reads and deserialises the blob entry for <paramref name="src"/>, returning
    /// <c>null</c> on a miss or if the entry is corrupt (in which case the corrupt
    /// entry is best-effort deleted so the cold-build path can rewrite it).
    /// </summary>
    private async Task<CompiledLibrary?> TryReadFromBlobAsync(LibrarySource src, CancellationToken ct)
    {
        try
        {
            var bytes = await _store.ReadAsync(BlobKey(src), ct);
            if (bytes == null) return null;
            return JsonSerializer.Deserialize<CompiledLibrary>(bytes, _jsonOpts);
        }
        catch
        {
            // Corrupt entry — drop it. Cold-build path will rewrite.
            try { await _store.DeleteAsync(BlobKey(src), ct); } catch { }
            return null;
        }
    }

    /// <summary>
    /// Best-effort serialisation of <paramref name="lib"/> to the blob store under
    /// <see cref="BlobKey(CompiledLibrary)"/>. Swallows host store failures so a
    /// flaky cache layer never breaks an otherwise-successful build.
    /// </summary>
    private async Task TryWriteToBlobAsync(CompiledLibrary lib, CancellationToken ct)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(lib, _jsonOpts);
            await _store.WriteAsync(BlobKey(lib), bytes, ct);
        }
        catch
        {
            // Persistence is best-effort; never fail the build because
            // the host's blob store complained.
        }
    }

    /// <summary>Stable cache key for a library source — <c>libcache/&lt;name&gt;.json</c>.</summary>
    private static string BlobKey(LibrarySource src)  => $"libcache/{src.LibraryName}.json";
    /// <summary>Stable cache key for an already-compiled library — <c>libcache/&lt;name&gt;.json</c>.</summary>
    private static string BlobKey(CompiledLibrary lib) => $"libcache/{lib.LibraryName}.json";
}
