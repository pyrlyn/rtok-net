using System.Text.RegularExpressions;

namespace Rtok.Proxy;

/// <summary>The must-keep spans of an archived tool result and the guard that rejects a summary
/// which drops one (rtok T385.9.1). Only the extraction is code, so a span is found the same way
/// in the prompt, the guard and the tests.</summary>
internal static partial class MustKeep
{
    /// <summary>The span kinds of rtok's P28 fixture.</summary>
    public static readonly string[] Kinds =
    [
        "path",
        "identifier",
        "error",
        "number",
        "command",
        "id",
    ];

    /// <summary>Longest span kept whole; a longer line is cut on a character boundary, so the
    /// guard asks the compressor for its first 200 characters only.</summary>
    public const int SpanChars = 200;

    private const int Timeout = 1000;

    /// <summary>(kind, regex) for the token-shaped spans; error lines and <c>$ </c> commands
    /// are line rules.</summary>
    private static readonly (string Kind, Regex Re)[] Tokens =
    [
        // file:line[:col] locations, bare relative file paths and diff hunk headers.
        ("path", Location()),
        ("path", RelativePath()),
        ("path", Hunk()),
        // Backticked names, `a::b` paths, CONST_CASE and declarations.
        ("identifier", Backticked()),
        ("identifier", ScopedName()),
        ("identifier", ConstCase()),
        ("identifier", Declaration()),
        // Compiler, exit and HTTP status codes.
        ("error", ErrorCode()),
        ("error", StatusCode()),
        // Quantities: units, decimals, long integers, `key=7` and `"key": 7` pairs and counted
        // outcomes.
        ("number", Unit()),
        ("number", LongNumber()),
        ("number", Pair()),
        ("number", Counted()),
        // Hex digests, prefixed ids (`req_7f3a`, `bat_01HZ…`, `leg_0003`) and ip:port.
        ("id", HexOrPrefixed()),
        ("id", IpPort()),
    ];

    /// <summary>Every must-keep span of <paramref name="text"/>, each once, in order of
    /// appearance.</summary>
    public static List<MustKeepSpan> Spans(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var spans = new List<MustKeepSpan>();
        void Push(string kind, string s)
        {
            s = Cut(s.Trim());
            if (s.Length > 0 && seen.Add(s))
            {
                spans.Add(new MustKeepSpan(kind, s));
            }
        }
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (ErrorLine().IsMatch(line))
            {
                Push("error", line);
            }
            if (line.StartsWith("$ ", StringComparison.Ordinal))
            {
                Push("command", line[2..]);
            }
            foreach (var (kind, re) in Tokens)
            {
                foreach (Match m in re.Matches(line))
                {
                    Push(
                        string.Equals(kind, "identifier", StringComparison.Ordinal)
                        && IsCommand(m.Value)
                            ? "command"
                            : kind,
                        m.Value
                    );
                }
            }
        }
        return spans;
    }

    /// <summary>The spans that <paramref name="summary"/> does not contain verbatim; empty means
    /// it keeps them all.</summary>
    public static List<MustKeepSpan> Dropped(IEnumerable<MustKeepSpan> spans, string summary) =>
        [.. spans.Where(s => !summary.Contains(s.Text, StringComparison.Ordinal))];

    /// <summary>A backticked invocation with a flag in it (<c>`acme settle --retry x`</c>) is a
    /// command.</summary>
    private static bool IsCommand(string token) =>
        token.StartsWith('`') && token.Contains(" -", StringComparison.Ordinal);

    /// <summary>The first <see cref="SpanChars"/> Unicode scalars of <paramref name="s"/>.</summary>
    private static string Cut(string s)
    {
        var (n, at) = (0, 0);
        foreach (var rune in s.EnumerateRunes())
        {
            if (n++ == SpanChars)
            {
                return s[..at];
            }
            at += rune.Utf16SequenceLength;
        }
        return s;
    }

    [GeneratedRegex(
        @"^(?:error|Error|ERROR|fatal|FATAL|FAILED|panicked|thread '[^']*' panicked)\b",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex ErrorLine();

    [GeneratedRegex(
        @"[\w./\\-]*[\w-]\.[A-Za-z][A-Za-z0-9]{0,5}:\d+(?::\d+)?",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex Location();

    [GeneratedRegex(
        @"\b[\w.-]+(?:/[\w.-]+)+\.[A-Za-z]{1,6}\b",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex RelativePath();

    [GeneratedRegex(@"^@@ .*? @@", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex Hunk();

    [GeneratedRegex(@"`[^`\n]{1,80}`", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex Backticked();

    [GeneratedRegex(@"\b[A-Za-z_]\w*(?:::\w+)+", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex ScopedName();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex ConstCase();

    [GeneratedRegex(
        @"\b(?:fn|def|class|struct|enum|trait|impl|func) [A-Za-z_]\w*",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\bE\d{4}\b|\bos error \d+", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex ErrorCode();

    [GeneratedRegex(
        @"\b(?:status|returned|exited with status|code) \d{3}\b",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex StatusCode();

    [GeneratedRegex(
        @"\b\d+(?:\.\d+)?\s?(?:ms|s|MB|KB|GB|%)\b",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex Unit();

    [GeneratedRegex(@"\b\d+\.\d{2,}\b|\b\d[\d_]{4,}\b", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"\b\w+=\d+\b|""\w+"": \d+", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex Pair();

    [GeneratedRegex(
        @"\b\d+ (?:passed|failed|ignored|errors?|warnings?|attempts?|retries)\b",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex Counted();

    [GeneratedRegex(
        @"\b[0-9a-f]{12,}\b|\b[a-z]{2,6}_[0-9A-Za-z]*\d[0-9A-Za-z]{3,}\b",
        RegexOptions.CultureInvariant,
        Timeout
    )]
    private static partial Regex HexOrPrefixed();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}:\d+\b", RegexOptions.CultureInvariant, Timeout)]
    private static partial Regex IpPort();
}
