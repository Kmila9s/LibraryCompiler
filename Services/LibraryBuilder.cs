using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LibraryCompiler.Repositories;
using Parser.Models;

namespace LibraryCompiler.Services;

/// <summary>
/// Compiles a single <see cref="LibrarySource"/> into a
/// <see cref="CompiledLibrary"/>. v1 extracts public symbol names by
/// regex and tags them as black boxes — exactly what
/// <c>IEEELibraryLoader</c> used to do, but exposed as a first-class
/// operation rather than buried in the Interpreter. v2 (queued, see #22's
/// follow-up tasks) replaces the regex pass with a real parser run and
/// adds primitive lowering.
/// </summary>
public sealed class LibraryBuilder
{
    // Function / procedure declaration matching. The Singleline flag makes
    // `\s` cross newlines so signatures wrapped across multiple lines (a
    // common style for long parameter lists) are still picked up. We
    // explicitly exclude the `procedure` keyword sitting inside a function
    // body and skip operator-overload names in quotes — those carry
    // semantic meaning to the synth path that the regex pass can't capture
    // accurately, and they're handled by the dedicated IEEE primitives
    // table when relevant.
    /// <summary>Regex that picks up <c>function NAME (…)</c> and <c>function NAME return …</c> declarations across multi-line wrapping.</summary>
    private static readonly Regex FnRe = new(
        @"(?ims)(?:^|;\s*|\bend\b\s*;\s*)\s*function\s+([a-z_][a-z0-9_]*)\s*(?:\(|return\b)",
        RegexOptions.Compiled);
    /// <summary>Regex that picks up <c>procedure NAME (…)</c> declarations.</summary>
    private static readonly Regex ProcRe = new(
        @"(?ims)(?:^|;\s*|\bend\b\s*;\s*)\s*procedure\s+([a-z_][a-z0-9_]*)\s*\(",
        RegexOptions.Compiled);
    // `subtype` and `type` declarations — the trailing `is` is on the same
    // line in every IEEE-canonical formatting we care about; if a user
    // wraps it weirdly we'll miss it but they're allowed to. Conservative
    // miss is better than spurious matches inside a comment block.
    /// <summary>Regex that picks up <c>type NAME is …</c> and <c>subtype NAME is …</c> declarations.</summary>
    private static readonly Regex TypeRe = new(
        @"(?im)^\s*(?:sub)?type\s+([a-z_][a-z0-9_]*)\s+is\b",
        RegexOptions.Compiled);

    /// <summary>Host-supplied options bundle (source opener, cache, policy, additional sources).</summary>
    private readonly LibraryCompilerOptions _options;

    /// <summary>Constructs a builder bound to the host's options.</summary>
    public LibraryBuilder(LibraryCompilerOptions options) => _options = options;

    /// <summary>
    /// Reads <paramref name="source"/>'s bytes through the host callback,
    /// extracts public symbols, and returns a <see cref="CompiledLibrary"/>.
    /// Falls back to a hard-coded canonical name list when the host can't
    /// supply source bytes — that path keeps trimmed deployments working
    /// even without the .vhd files on disk.
    /// </summary>
    public Task<CompiledLibrary> BuildAsync(LibrarySource source, CancellationToken ct = default)
        => BuildAsync(source, preReadBytes: null, ct);

    /// <summary>
    /// Builds with source bytes that the caller already read — used by the
    /// orchestrator's persistent-cache path so we don't pay the source
    /// round-trip twice when validating a cache hit.
    /// </summary>
    public async Task<CompiledLibrary> BuildAsync(LibrarySource source, byte[]? preReadBytes, CancellationToken ct = default)
    {
        var diagnostics = new List<LibraryDiagnostic>();

        byte[]? bytes = preReadBytes ?? await ReadSourceBytesAsync(source, ct);
        if (bytes == null)
        {
            diagnostics.Add(new LibraryDiagnostic(
                LibraryDiagnosticSeverity.Warning,
                "LIB-NO-SOURCE",
                source.LibraryName,
                $"No source available for library '{source.LibraryName}' " +
                "(vendor key '" + source.SourceKey + "'). Falling back to " +
                "the canonical IEEE name list — user code parses but the " +
                "synth-time primitives are still hard-coded shims."));
            return BuildFallback(source, diagnostics);
        }

        string text = System.Text.Encoding.UTF8.GetString(bytes);
        string hash = Sha256Hex(bytes);

        // Comments contain prose like "function" or "type" that would
        // otherwise produce false-positive symbols (VHDL ≤ 2008 has only
        // line comments). String-aware, like the parser.
        string cleaned = VhdlText.StripLineComments(text);

        // De-duplicate by (name, kind) so an overloaded function declared
        // multiple times (common in IEEE stubs — different argument types)
        // appears once in the registry. The synth-time primitive table
        // handles overload resolution; the registry just needs the name.
        var seen = new HashSet<(string, LibrarySymbolKind)>();
        var symbols = new List<LibrarySymbol>();
        void AddOnce(string name, LibrarySymbolKind kind)
        {
            if (seen.Add((name, kind)))
                symbols.Add(new LibrarySymbol(name, kind, IsBlackbox: true));
        }

        foreach (Match m in FnRe.Matches(cleaned))   AddOnce(m.Groups[1].Value, LibrarySymbolKind.Function);
        foreach (Match m in ProcRe.Matches(cleaned)) AddOnce(m.Groups[1].Value, LibrarySymbolKind.Procedure);
        foreach (Match m in TypeRe.Matches(cleaned)) AddOnce(m.Groups[1].Value, LibrarySymbolKind.Type);

        return new CompiledLibrary(
            LibraryName:  source.LibraryName,
            Vendor:       source.Vendor,
            ContentHash:  hash,
            Functions:    symbols,
            Diagnostics:  diagnostics);
    }

    /// <summary>
    /// Reads the source bytes the host can supply, or falls back to the
    /// embedded resource for IEEE builtins. Returns <c>null</c> when the
    /// library is genuinely unsourced. Marked <c>internal</c> so the
    /// orchestrator can read bytes once and reuse them across the
    /// hash-validation + cold-build paths.
    /// </summary>
    internal async Task<byte[]?> ReadSourceBytesAsync(LibrarySource source, CancellationToken ct)
    {
        // 1. Ask the host first — its policy may shadow embedded builtins
        //    with a user-provided override.
        try
        {
            using var stream = await _options.OpenLibrarySource(source.SourceKey, ct);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
        }
        catch
        {
            // Host callback misbehaved — fall through to the embedded
            // builtin so trimmed deployments still parse user code.
        }

        // 2. Embedded resource fallback for IEEE builtins.
        if (source.Vendor.Equals("IEEE", StringComparison.OrdinalIgnoreCase))
        {
            var asm = typeof(LibraryBuilder).Assembly;
            string? resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(
                    Path.GetFileName(source.SourceKey),
                    StringComparison.OrdinalIgnoreCase));
            if (resName != null)
            {
                using var rs = asm.GetManifestResourceStream(resName);
                if (rs != null)
                {
                    using var ms = new MemoryStream();
                    await rs.CopyToAsync(ms, ct);
                    return ms.ToArray();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Hard-coded fallback for IEEE.std_logic_1164 / IEEE.numeric_std when
    /// no source bytes are available. Mirrors the legacy
    /// <c>IEEELibraryLoader.SeedFallbackNames</c> behaviour.
    /// </summary>
    private static CompiledLibrary BuildFallback(LibrarySource source, List<LibraryDiagnostic> diagnostics)
    {
        string[] names = source.LibraryName.ToLowerInvariant() switch
        {
            "std_logic_1164" => new[]
            {
                "rising_edge", "falling_edge",
                "to_x01", "to_x01z", "to_ux01", "is_x"
            },
            "numeric_std" => new[]
            {
                "to_integer", "to_unsigned", "to_signed", "resize",
                "shift_left", "shift_right", "rotate_left", "rotate_right",
                "conv_integer", "conv_std_logic_vector"
            },
            "math_real" => new[]
            {
                "ceil", "floor", "round", "trunc", "realmax", "realmin",
                "sqrt", "cbrt", "exp", "log", "log2", "log10",
                "sin", "cos", "tan", "arcsin", "arccos", "arctan",
                "sinh", "cosh", "tanh", "arcsinh", "arccosh", "arctanh"
            },
            "std_logic_arith" => new[]
            {
                "conv_integer", "conv_unsigned", "conv_signed",
                "conv_std_logic_vector",
                "shl", "shr", "ext", "sxt"
            },
            "std_logic_unsigned" => new[]
            {
                "conv_integer", "shl", "shr"
            },
            "std_logic_signed" => new[]
            {
                "conv_integer", "shl", "shr", "abs"
            },
            _ => Array.Empty<string>()
        };

        var symbols = names
            .Select(n => new LibrarySymbol(n, LibrarySymbolKind.Function, IsBlackbox: true))
            .ToList();

        return new CompiledLibrary(
            LibraryName: source.LibraryName,
            Vendor:      source.Vendor,
            ContentHash: $"fallback-{source.LibraryName}",
            Functions:   symbols,
            Diagnostics: diagnostics);
    }

    /// <summary>
    /// SHA-256 of the source bytes, encoded as upper-case hex. Used as the
    /// content hash in <see cref="CompiledLibrary.ContentHash"/> and as the
    /// cache key in <see cref="BuildOrchestrator"/>'s blob-store layer.
    /// </summary>
    /// <param name="bytes">Source bytes to hash.</param>
    /// <returns>Upper-case hex SHA-256 digest.</returns>
    internal static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
