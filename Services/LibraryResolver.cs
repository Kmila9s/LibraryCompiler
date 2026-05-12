using LibraryCompiler.Repositories;

namespace LibraryCompiler.Services;

/// <summary>
/// Maps a logical library name (as it appears in <c>use X.Y.all</c>) to a
/// <see cref="LibrarySource"/> the orchestrator can hand to a builder.
/// Knows about the bundled IEEE builtins and the host's user-supplied
/// additional sources; nothing else.
/// </summary>
/// <remarks>
/// All mutations and reads go through <see cref="_lock"/> because the
/// host can re-sync user-imported entries (Settings → Libraries import
/// or delete) while a parallel build is concurrently consulting the
/// resolver from <see cref="ResolveTransitive"/>.
/// </remarks>
public sealed class LibraryResolver
{
    /// <summary>
    /// Library catalog keyed by case-insensitive logical name (as it appears in
    /// <c>use X.Y.all</c>). Mutated by <see cref="Register"/> / <see cref="Unregister"/>
    /// and read by <see cref="Resolve"/> / <see cref="All"/> / <see cref="ResolveTransitive"/>.
    /// All access is guarded by <see cref="_lock"/>.
    /// </summary>
    private readonly Dictionary<string, LibrarySource> _byName =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Mutex guarding <see cref="_byName"/> against concurrent host re-syncs and parallel resolution.</summary>
    private readonly object _lock = new();

    /// <summary>
    /// Initializes the resolver with the bundled IEEE builtins plus any
    /// host-supplied user libraries. Later registrations win over earlier
    /// ones with the same name — that lets a user override an IEEE stub
    /// with their own implementation if they really want to.
    /// </summary>
    public LibraryResolver(IReadOnlyList<LibrarySource>? additionalSources = null)
    {
        // Bundled IEEE builtins. The SourceKey points at the embedded-
        // resource name; the host's OpenLibrarySource callback decides
        // whether to read from the embedded stream or substitute its own
        // copy from disk.
        Register(new LibrarySource(
            LibraryName: "std_logic_1164",
            Vendor:      "IEEE",
            SourceKey:   "Builtins/IEEE/std_logic_1164.vhd",
            DependsOn:   Array.Empty<string>()));
        Register(new LibrarySource(
            LibraryName: "numeric_std",
            Vendor:      "IEEE",
            SourceKey:   "Builtins/IEEE/numeric_std.vhd",
            DependsOn:   new[] { "std_logic_1164" }));

        // IEEE.math_real — real-arithmetic helpers (sin / cos / log / sqrt / …).
        // No dependency on std_logic_1164: math_real operates on REAL, not on
        // multivalue logic types.
        Register(new LibrarySource(
            LibraryName: "math_real",
            Vendor:      "IEEE",
            SourceKey:   "Builtins/IEEE/math_real.vhd",
            DependsOn:   Array.Empty<string>()));

        // Synopsys legacy packages still common in classroom material.
        // Each declares its own unsigned/signed types but ultimately layers
        // on std_logic_1164 for the bit-level encoding.
        Register(new LibrarySource(
            LibraryName: "std_logic_arith",
            Vendor:      "IEEE",
            SourceKey:   "Builtins/IEEE/std_logic_arith.vhd",
            DependsOn:   new[] { "std_logic_1164" }));
        Register(new LibrarySource(
            LibraryName: "std_logic_unsigned",
            Vendor:      "IEEE",
            SourceKey:   "Builtins/IEEE/std_logic_unsigned.vhd",
            DependsOn:   new[] { "std_logic_1164" }));
        Register(new LibrarySource(
            LibraryName: "std_logic_signed",
            Vendor:      "IEEE",
            SourceKey:   "Builtins/IEEE/std_logic_signed.vhd",
            DependsOn:   new[] { "std_logic_1164" }));

        if (additionalSources != null)
            foreach (var s in additionalSources) Register(s);
    }

    /// <summary>Adds or replaces a library entry by name.</summary>
    public void Register(LibrarySource source)
    {
        lock (_lock) _byName[source.LibraryName] = source;
    }

    /// <summary>
    /// Removes the entry matching <paramref name="libraryName"/>, returning
    /// <c>true</c> if it existed. Used by the host on Settings → Libraries
    /// delete (and on external file removals picked up via
    /// <c>UserAssetStore.OnChange</c>) to keep the resolver's view of the
    /// world honest.
    /// </summary>
    public bool Unregister(string libraryName)
    {
        if (string.IsNullOrEmpty(libraryName)) return false;
        lock (_lock) return _byName.Remove(libraryName);
    }

    /// <summary>Looks up a library by name, returning <c>null</c> when unknown.</summary>
    public LibrarySource? Resolve(string libraryName)
    {
        lock (_lock)
            return _byName.TryGetValue(libraryName, out var s) ? s : null;
    }

    /// <summary>
    /// Snapshot of every library currently known to this resolver. Returns
    /// a copy so the caller can iterate without holding the resolver's
    /// internal lock.
    /// </summary>
    public IReadOnlyCollection<LibrarySource> All
    {
        get { lock (_lock) return _byName.Values.ToArray(); }
    }

    /// <summary>
    /// Returns the libraries listed in <paramref name="requested"/> plus
    /// every library they transitively depend on, in topological order
    /// (dependencies before dependents). Unknown names are silently
    /// skipped — the caller surfaces a diagnostic for those.
    /// </summary>
    public IReadOnlyList<LibrarySource> ResolveTransitive(IEnumerable<string> requested)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<LibrarySource>();

        // Snapshot under the lock so the recursive walk doesn't see a torn
        // dictionary if OnChange reconciles user libraries mid-traversal.
        Dictionary<string, LibrarySource> snapshot;
        lock (_lock) snapshot = new(_byName, StringComparer.OrdinalIgnoreCase);

        foreach (var name in requested) Visit(name);
        return ordered;

        void Visit(string name)
        {
            if (!visited.Add(name)) return;
            if (!snapshot.TryGetValue(name, out var src)) return;
            foreach (var dep in src.DependsOn) Visit(dep);
            ordered.Add(src);
        }
    }
}
