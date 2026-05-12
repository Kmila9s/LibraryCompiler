namespace LibraryCompiler.Services;

/// <summary>
/// In-memory retention strategy for compiled artifacts. Combined with the
/// host-supplied <see cref="IBlobStore"/> (or a <see cref="NullBlobStore"/>)
/// to express the full cache policy.
/// </summary>
public enum LibraryBuildMode
{
    /// <summary>
    /// Re-parse every invocation; nothing retained. Lowest RAM, lowest
    /// disk, highest CPU. Safe default for low-spec devices.
    /// </summary>
    OnDemand,

    /// <summary>
    /// Keep compiled artifacts in memory for the lifetime of the process.
    /// Subsequent compiles in the same session are dictionary lookups.
    /// </summary>
    ProcessCached,

    /// <summary>
    /// Keep in memory AND read/write through the host's
    /// <see cref="IBlobStore"/> so artifacts survive across sessions.
    /// </summary>
    Persistent
}

/// <summary>
/// Per-library override for the global <see cref="LibraryBuildMode"/>. The
/// host typically picks a global default (e.g. <c>ProcessCached</c>) and
/// supplies overrides for libraries that want different behaviour
/// (e.g. keep IEEE process-cached but force one heavy custom library to
/// build OnDemand to spare RAM).
/// </summary>
public sealed class LibraryBuildPolicy
{
    /// <summary>
    /// Mode applied to libraries with no explicit override. Default is
    /// <see cref="LibraryBuildMode.ProcessCached"/>; the host should
    /// downgrade to <see cref="LibraryBuildMode.OnDemand"/> on devices
    /// where probed free RAM is below its chosen threshold.
    /// </summary>
    public LibraryBuildMode Default { get; init; } = LibraryBuildMode.ProcessCached;

    /// <summary>Per-library overrides keyed by case-insensitive library name.</summary>
    public IReadOnlyDictionary<string, LibraryBuildMode> PerLibrary { get; init; }
        = new Dictionary<string, LibraryBuildMode>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the effective mode for a given library name.</summary>
    public LibraryBuildMode ResolveFor(string libraryName)
        => PerLibrary.TryGetValue(libraryName, out var m) ? m : Default;

    /// <summary>Convenience: every library uses <see cref="LibraryBuildMode.OnDemand"/>.</summary>
    public static LibraryBuildPolicy AlwaysOnDemand
        => new() { Default = LibraryBuildMode.OnDemand };

    /// <summary>Convenience: every library uses <see cref="LibraryBuildMode.ProcessCached"/>.</summary>
    public static LibraryBuildPolicy AlwaysProcessCached
        => new() { Default = LibraryBuildMode.ProcessCached };

    /// <summary>Convenience: every library uses <see cref="LibraryBuildMode.Persistent"/>.</summary>
    public static LibraryBuildPolicy AlwaysPersistent
        => new() { Default = LibraryBuildMode.Persistent };
}
