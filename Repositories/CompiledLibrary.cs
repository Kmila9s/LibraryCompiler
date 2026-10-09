namespace LibraryCompiler.Repositories;

/// <summary>
/// One compiled library — what a build pass returns to the consumer.
/// Pure data: the function names, the (eventual) parsed bodies, the
/// content hash for cache key derivation, plus diagnostics produced
/// during the build.
/// </summary>
/// <param name="LibraryName">Same value as the input <see cref="LibrarySource.LibraryName"/>.</param>
/// <param name="Vendor">Same value as <see cref="LibrarySource.Vendor"/>.</param>
/// <param name="ContentHash">
/// SHA-256 of the source bytes that were compiled. Used as the cache key
/// under the Persistent build policy and as the staleness check when
/// re-resolving across sessions.
/// </param>
/// <param name="Functions">
/// Public function / procedure signatures the library exposes. v1 holds
/// names + package tags only; v2 (queued) replaces this with parsed bodies.
/// </param>
/// <param name="Diagnostics">
/// Any warnings or errors the build produced. Stable code strings (e.g.
/// <c>VHD-FUNC-BLACKBOX</c>) so the editor can match them to documentation.
/// </param>
public sealed record CompiledLibrary(
    string LibraryName,
    string Vendor,
    string ContentHash,
    IReadOnlyList<LibrarySymbol> Functions,
    IReadOnlyList<LibraryDiagnostic> Diagnostics);

/// <summary>One public symbol from a compiled library.</summary>
/// <param name="Name">Symbol identifier (case-insensitive in VHDL).</param>
/// <param name="Kind">What kind of symbol — function, procedure, type, constant, …</param>
/// <param name="IsBlackbox">
/// True when the compiler accepted the declaration but doesn't yet have a
/// real synthesizable body. v1 is all-blackbox; v2 lowers to primitives.
/// </param>
public sealed record LibrarySymbol(
    string Name,
    LibrarySymbolKind Kind,
    bool IsBlackbox);

/// <summary>Categories of symbol a library can publish.</summary>
public enum LibrarySymbolKind
{
    /// <summary>Subprogram returning a value (functions).</summary>
    Function,
    /// <summary>Subprogram with side effects (procedures).</summary>
    Procedure,
    /// <summary>Type or subtype declaration (std_logic, unsigned, …).</summary>
    Type,
    /// <summary>Constant declaration.</summary>
    Constant
}

/// <summary>Build-time diagnostic emitted by the library compiler.</summary>
/// <param name="Severity">Info, Warning, or Error.</param>
/// <param name="Code">Stable code string (e.g. <c>LIB-PARSE-FAIL</c>).</param>
/// <param name="Subject">The library / symbol the diagnostic is about.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record LibraryDiagnostic(
    LibraryDiagnosticSeverity Severity,
    string Code,
    string Subject,
    string Message);

/// <summary>Severity buckets matching the editor's marker levels.</summary>
public enum LibraryDiagnosticSeverity
{
    /// <summary>Informational diagnostic.</summary>
    Info,
    /// <summary>Warning diagnostic.</summary>
    Warning,
    /// <summary>Error diagnostic.</summary>
    Error
}
