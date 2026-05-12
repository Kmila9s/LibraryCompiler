using LibraryCompiler.Repositories;

namespace LibraryCompiler.Services;

/// <summary>
/// Top-level facade the host (Interpreter / Debugger / future MAUI app)
/// wires up via DI. Encapsulates the resolver, the builder, the
/// orchestrator, and the policy. The host's only responsibility is
/// constructing <see cref="LibraryCompilerOptions"/> with appropriate
/// callbacks and then calling <see cref="CompileAsync(IEnumerable{string}, BuildExecutionMode, CancellationToken)"/>.
/// </summary>
public sealed class LibraryCompiler
{
    /// <summary>Build orchestrator that owns the resolver, builder, cache, and execution strategy.</summary>
    private readonly BuildOrchestrator _orchestrator;
    /// <summary>Convenience handle to the orchestrator's <see cref="LibraryResolver"/>.</summary>
    private readonly LibraryResolver _resolver;

    /// <summary>The resolver — exposed for tests and for hosts that want to enumerate known libraries.</summary>
    public LibraryResolver Resolver => _resolver;

    /// <summary>The orchestrator — exposed for the in-memory cache snapshot / Reset hook.</summary>
    public BuildOrchestrator Orchestrator => _orchestrator;

    /// <summary>
    /// Constructs a compiler with the host-supplied options. Throws
    /// <see cref="ArgumentNullException"/> if <see cref="LibraryCompilerOptions.OpenLibrarySource"/>
    /// is null — the lib has no business probing the filesystem on its own.
    /// </summary>
    public LibraryCompiler(LibraryCompilerOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.OpenLibrarySource is null)
            throw new ArgumentException(
                "LibraryCompilerOptions.OpenLibrarySource must be supplied — " +
                "the LibraryCompiler module never decides where source bytes " +
                "come from. Pass at minimum a callback that returns null for " +
                "every key (the embedded IEEE builtins will still be reachable).",
                nameof(options));

        _orchestrator = new BuildOrchestrator(options);
        _resolver     = _orchestrator.Resolver;
    }

    /// <summary>
    /// Convenience overload that takes a synchronous source-stream
    /// callback — common for tests and for hosts that already have the
    /// bytes in memory.
    /// </summary>
    public static LibraryCompiler FromSyncOpener(
        Func<string, Stream?> openSync,
        IBlobStore? cacheStore = null,
        IReadOnlyList<LibrarySource>? additionalSources = null,
        LibraryBuildPolicy? policy = null)
        => new(new LibraryCompilerOptions(
            OpenLibrarySource: (key, ct) => Task.FromResult(openSync(key)),
            CacheStore:        cacheStore,
            AdditionalSources: additionalSources,
            Policy:            policy));

    /// <summary>
    /// Compiles every library in <paramref name="requested"/> plus their
    /// transitive dependencies. Returns the full set in topological order
    /// (linear) or library-name order (parallel).
    /// </summary>
    public Task<IReadOnlyList<CompiledLibrary>> CompileAsync(
        IEnumerable<string> requested,
        BuildExecutionMode execution = BuildExecutionMode.Linear,
        CancellationToken ct = default)
        => _orchestrator.BuildAsync(requested, execution, ct);

    /// <summary>
    /// Compiles a complete project — list of source files plus their
    /// declared library dependencies — into a <see cref="CompiledProject"/>.
    /// Real FPGA designs span many .vhd files; this is the entry point
    /// the editor's "Build" button calls and the test runners can use
    /// for multi-file fixtures.
    /// </summary>
    public async Task<CompiledProject> CompileProjectAsync(
        ProjectContext project,
        BuildExecutionMode execution = BuildExecutionMode.Linear,
        CancellationToken ct = default)
    {
        // 1. Build every library the project asked for.
        var libs = await CompileAsync(project.UseClauses, execution, ct);

        // 2. Pass-through the user files. Actual VHDL parsing of each
        //    file is the Interpreter's job — this module's contract is
        //    library compilation, not user-code compilation. The host
        //    receives the merged result and feeds it into the existing
        //    Parser → Synthesizer pipeline.
        return new CompiledProject(
            project.Files,
            libs,
            DiagnosticsFromLibs(libs));
    }

    /// <summary>
    /// Flattens the per-library diagnostics into a single list for the project compile result.
    /// </summary>
    /// <param name="libs">Compiled libraries returned by <see cref="CompileAsync"/>.</param>
    /// <returns>Concatenated <see cref="LibraryDiagnostic"/> sequence.</returns>
    private static IReadOnlyList<LibraryDiagnostic> DiagnosticsFromLibs(IReadOnlyList<CompiledLibrary> libs)
    {
        var all = new List<LibraryDiagnostic>();
        foreach (var l in libs) all.AddRange(l.Diagnostics);
        return all;
    }
}

/// <summary>
/// Input to <see cref="LibraryCompiler.CompileProjectAsync"/>. Captures
/// the multi-file nature of real FPGA projects: a list of user-source
/// files plus the libraries they collectively reference.
/// </summary>
/// <param name="Files">Absolute paths or host-defined keys for each user source file.</param>
/// <param name="UseClauses">
/// Library names from <c>use X.Y.all</c> across every file. The host
/// pre-scans the user code (cheap regex) and supplies the union.
/// </param>
/// <param name="TopEntity">
/// Optional name of the top-level entity. Hosts that don't know yet pass
/// <c>null</c>; the editor sets it once the user picks a top.
/// </param>
public sealed record ProjectContext(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> UseClauses,
    string? TopEntity = null);

/// <summary>Result of a multi-file project compile.</summary>
/// <param name="UserFiles">The original list of user source files.</param>
/// <param name="Libraries">Compiled IEEE / user-imported libraries.</param>
/// <param name="Diagnostics">Aggregated diagnostics from every library build.</param>
public sealed record CompiledProject(
    IReadOnlyList<string> UserFiles,
    IReadOnlyList<CompiledLibrary> Libraries,
    IReadOnlyList<LibraryDiagnostic> Diagnostics);
