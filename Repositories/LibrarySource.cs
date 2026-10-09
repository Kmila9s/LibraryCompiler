namespace LibraryCompiler.Repositories;

/// <summary>
/// Descriptor for one VHDL library the host wants the compiler to make
/// available. Holds enough information to (a) identify the library by name
/// for <c>use X.Y.all</c> resolution, (b) locate its source bytes through
/// a host-supplied callback, and (c) order builds correctly when one
/// library depends on another.
/// </summary>
/// <param name="LibraryName">
/// Logical name as it appears in user code (e.g. <c>std_logic_1164</c>,
/// <c>numeric_std</c>, <c>my_crc_pkg</c>). Case-insensitive matched.
/// </param>
/// <param name="Vendor">
/// Library vendor / namespace. Defaults to <c>"IEEE"</c> for the bundled
/// builtins; user libraries typically use <c>"WORK"</c> or a project name.
/// </param>
/// <param name="SourceKey">
/// Opaque key the compiler hands back to the host's
/// <see cref="LibraryCompiler.Services.LibraryCompilerOptions.OpenLibrarySource"/> callback when it
/// wants the source bytes. The host decides whether this is a relative
/// path, an embedded-resource name, an absolute file path, or anything
/// else — the compiler treats it as a black box string.
/// </param>
/// <param name="DependsOn">
/// Other library names this library expects to resolve before it builds.
/// <c>numeric_std</c> declares <c>std_logic_1164</c> here so the
/// orchestrator can topologically sort the build queue.
/// </param>
public sealed record LibrarySource(
    string LibraryName,
    string Vendor,
    string SourceKey,
    IReadOnlyList<string> DependsOn);
