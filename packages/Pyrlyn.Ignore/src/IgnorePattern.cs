using System.Text;
using System.Text.RegularExpressions;

namespace Pyrlyn.Ignore;

/// <summary>
/// One line of a gitignore file, matched the way git matches it (gitignore(5), git's wildmatch
/// with <c>WM_PATHNAME</c>): <c>*</c>, <c>?</c> and brackets never cross <c>/</c>; <c>**</c> spans
/// directories only as a whole leading, trailing or middle component; a pattern with a <c>/</c>
/// before its end is anchored to the file's directory, any other matches the basename at any depth.
/// </summary>
public sealed class IgnorePattern
{
    private static readonly TimeSpan Timeout = System.Threading.Timeout.InfiniteTimeSpan;

    private readonly string glob;
    private readonly Kind kind;
    private readonly string literal;
    private readonly Regex? regex;
    private readonly StringComparison comparison;

    private IgnorePattern(
        string glob,
        bool negated,
        bool directoryOnly,
        bool anchored,
        bool ignoreCase
    )
    {
        this.glob = glob;
        Negated = negated;
        DirectoryOnly = directoryOnly;
        Anchored = anchored;
        comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (glob.AsSpan().IndexOfAny("*?[\\") < 0)
        {
            kind = Kind.Literal;
            literal = glob;
        }
        else if (
            !anchored
            && glob.Length > 1
            && glob[0] == '*'
            && glob.AsSpan(1).IndexOfAny("*?[\\") < 0
        )
        {
            kind = Kind.Suffix;
            literal = glob[1..];
        }
        else
        {
            literal = "";
            var source = ToRegex(glob);
            if (source is null)
            {
                kind = Kind.Never;
            }
            else
            {
                kind = Kind.Regex;
                var options =
                    RegexOptions.CultureInvariant
                    | RegexOptions.NonBacktracking
                    | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                regex = new Regex(source, options, Timeout);
            }
        }
    }

    private enum Kind
    {
        Literal,
        Suffix,
        Regex,
        Never,
    }

    /// <summary>A <c>!</c> line: a path it names is kept.</summary>
    public bool Negated { get; }

    /// <summary>A line ending in <c>/</c>: it names directories only.</summary>
    public bool DirectoryOnly { get; }

    /// <summary>A <c>/</c> at the start or in the middle: matched against the path relative to the file's directory, not the basename.</summary>
    public bool Anchored { get; }

    /// <summary>
    /// Parse one gitignore line: <see langword="null"/> for a blank line or a <c>#</c> comment.
    /// Trailing spaces are dropped unless escaped with <c>\</c>, a trailing <c>\r</c> too.
    /// </summary>
    public static IgnorePattern? Parse(string line, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.EndsWith('\r'))
        {
            line = line[..^1];
        }
        line = TrimTrailingSpaces(line);
        if (line.Length == 0 || line[0] == '#')
        {
            return null;
        }
        var negated = line[0] == '!';
        if (negated)
        {
            line = line[1..];
        }
        var directoryOnly = line.EndsWith('/');
        if (directoryOnly)
        {
            line = line[..^1];
        }
        var anchored = line.Contains('/', StringComparison.Ordinal);
        if (line.StartsWith('/'))
        {
            line = line[1..];
        }
        return new IgnorePattern(line, negated, directoryOnly, anchored, ignoreCase);
    }

    /// <summary>
    /// Whether the pattern names <paramref name="relative"/>, a <c>/</c>-separated path relative
    /// to the directory of the file the pattern came from.
    /// </summary>
    public bool IsMatch(string relative, bool isDirectory)
    {
        ArgumentNullException.ThrowIfNull(relative);
        if (DirectoryOnly && !isDirectory)
        {
            return false;
        }
        var text = Anchored ? relative : relative[(relative.LastIndexOf('/') + 1)..];
        return kind switch
        {
            Kind.Literal => text.Equals(literal, comparison),
            Kind.Suffix => text.EndsWith(literal, comparison),
            Kind.Regex => regex!.IsMatch(text),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override string ToString() =>
        (Negated ? "!" : "") + (Anchored ? "/" : "") + glob + (DirectoryOnly ? "/" : "");

    /// <summary>git's <c>trim_trailing_spaces</c>: drop the trailing run of spaces unless the first is escaped.</summary>
    private static string TrimTrailingSpaces(string line)
    {
        int? lastSpace = null;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == ' ')
            {
                lastSpace ??= i;
                continue;
            }
            lastSpace = null;
            if (line[i] == '\\')
            {
                i++;
            }
        }
        return lastSpace is { } at ? line[..at] : line;
    }

    /// <summary>The glob as an anchored regex, or <see langword="null"/> when git would never match it (an unterminated bracket, a trailing backslash).</summary>
    private static string? ToRegex(string glob)
    {
        var re = new StringBuilder("^");
        var i = 0;
        while (i < glob.Length)
        {
            var c = glob[i];
            switch (c)
            {
                case '*':
                    i = Stars(glob, i, re);
                    continue;
                case '?':
                    re.Append("[^/]");
                    break;
                case '\\':
                    if (i + 1 == glob.Length)
                    {
                        return null;
                    }
                    i++;
                    re.Append(Regex.Escape(glob[i].ToString()));
                    break;
                case '[':
                {
                    var end = Bracket(glob, i, re);
                    if (end < 0)
                    {
                        return null;
                    }
                    i = end;
                    continue;
                }
                default:
                    re.Append(Regex.Escape(c.ToString()));
                    break;
            }
            i++;
        }
        return re.Append('$').ToString();
    }

    /// <summary>Translate the run of <c>*</c> at <paramref name="i"/>; the index past it (and past the slash a <c>**/</c> owns).</summary>
    private static int Stars(string glob, int i, StringBuilder re)
    {
        var stars = 1;
        while (i + stars < glob.Length && glob[i + stars] == '*')
        {
            stars++;
        }
        var atStart = i == 0 || glob[i - 1] == '/';
        var end = i + stars;
        if (stars >= 2 && atStart && end == glob.Length)
        {
            re.Append(".*");
        }
        else if (stars >= 2 && atStart && glob[end] == '/')
        {
            // Zero or more whole directories; the slash goes with them.
            re.Append("(?:.*/)?");
            end++;
        }
        else
        {
            re.Append("[^/]*");
        }
        return end;
    }

    /// <summary>Translate the bracket expression at <paramref name="start"/>; the index past its <c>]</c>, or -1 when it is malformed or can match nothing.</summary>
    private static int Bracket(string glob, int start, StringBuilder re)
    {
        var i = start + 1;
        var negate = i < glob.Length && glob[i] is '!' or '^';
        if (negate)
        {
            i++;
        }
        var members = new StringBuilder();
        var first = true;
        while (i < glob.Length && (glob[i] != ']' || first))
        {
            first = false;
            if (glob[i] == '[' && i + 1 < glob.Length && glob[i + 1] == ':')
            {
                var close = glob.IndexOf(":]", i + 2, StringComparison.Ordinal);
                if (close < 0 || PosixClass(glob[(i + 2)..close]) is not { } set)
                {
                    return -1;
                }
                members.Append(set);
                i = close + 2;
                continue;
            }
            if (!Member(glob, ref i, out var lo))
            {
                return -1;
            }
            if (i + 1 < glob.Length && glob[i] == '-' && glob[i + 1] != ']')
            {
                i++;
                if (!Member(glob, ref i, out var hi))
                {
                    return -1;
                }
                if (hi >= lo)
                {
                    members.Append(ClassChar(lo)).Append('-').Append(ClassChar(hi));
                }
                continue;
            }
            members.Append(ClassChar(lo));
        }
        if (i >= glob.Length)
        {
            return -1;
        }
        if (!Emit(re, negate, members))
        {
            return -1;
        }
        return i + 1;
    }

    /// <summary>Append the class; false when it can match nothing (only reversed ranges), so neither can the pattern.</summary>
    private static bool Emit(StringBuilder re, bool negate, StringBuilder members)
    {
        if (negate)
        {
            re.Append("[^/").Append(members).Append(']');
            return true;
        }
        if (members.Length == 0)
        {
            return false;
        }
        // Never a slash: class subtraction, since NonBacktracking has no lookaround.
        re.Append('[').Append(members).Append("-[/]]");
        return true;
    }

    private static bool Member(string glob, ref int i, out char c)
    {
        if (glob[i] == '\\')
        {
            i++;
            if (i >= glob.Length)
            {
                c = '\0';
                return false;
            }
        }
        c = glob[i];
        i++;
        return true;
    }

    private static string ClassChar(char c) =>
        c is '\\' or ']' or '[' or '^' or '-' ? "\\" + c : c.ToString();

    private static string? PosixClass(string name) =>
        name switch
        {
            "alnum" => "a-zA-Z0-9",
            "alpha" => "a-zA-Z",
            "blank" => " \\t",
            "cntrl" => "\\x00-\\x1F\\x7F",
            "digit" => "0-9",
            "graph" => "\\x21-\\x7E",
            "lower" => "a-z",
            "print" => "\\x20-\\x7E",
            "punct" => "!-/:-@\\[-`{-~",
            "space" => " \\t\\n\\r\\f\\v",
            "upper" => "A-Z",
            "xdigit" => "0-9A-Fa-f",
            _ => null,
        };
}
