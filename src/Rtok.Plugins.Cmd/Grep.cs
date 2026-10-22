using System.Text;
using System.Text.RegularExpressions;
using Rtok.Core;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// <c>grep</c> / <c>rg</c> / <c>git grep</c> hit lists (rtok T673): each path printed once with
/// its matches as <c>line:text</c> beneath it, the indentation the matches share dropped, a long
/// line cut to a window around the hit, and a per-file budget that ends in <c>… +N more</c>.
/// Returns null when the output is not the <c>path:line:text</c> shape, so the caller falls back
/// to the grep rule instead of guessing.
/// </summary>
internal static class Grep
{
    // Matches kept per file with their text.
    private const int PerFile = 12;

    // Output lines with match text; files after that are listed as bare `path:line,line`
    // anchors, so every path and line number survives a wide search.
    private const int Budget = 16;

    // Line numbers listed for one anchor-only file.
    private const int Anchors = 12;

    // Chars kept either side of the hit in a long line.
    private const int Window = 60;

    // A user pattern gets this long to find its hit in one line.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    // Flags that change the output shape (counts, file lists, context blocks): not ours.
    private static readonly HashSet<string> ShapeFlags = new(StringComparer.Ordinal)
    {
        "-c",
        "-l",
        "-L",
        "-q",
        "-o",
        "-A",
        "-B",
        "-C",
        "-f",
        "-z",
        "-Z",
        "--count",
        "--files-with-matches",
        "--files-without-match",
        "--only-matching",
        "--context",
        "--after-context",
        "--before-context",
        "--null",
        "--heading",
        "--json",
        "--files",
        "--no-filename",
        "-h",
        "--file",
    };

    // Short flags that take a value in the next argv slot.
    private const string ValueShort = "mgtTdDM";

    // Long flags whose value is the next argv slot (`--type rust`), so it is not the pattern.
    private static readonly HashSet<string> ValueLong = new(StringComparer.Ordinal)
    {
        "type",
        "type-not",
        "glob",
        "iglob",
        "max-count",
        "include",
        "exclude",
        "exclude-dir",
        "max-depth",
        "threads",
        "ignore-file",
        "replace",
        "sort",
        "sortr",
    };

    /// <summary>The hit list regrouped by path, or null for any other shape or when it would
    /// not be shorter.</summary>
    public static string? Group(IReadOnlyList<string> argv, string output)
    {
        if (ParseArgv(argv) is not var (flags, pattern))
        {
            return null;
        }

        var numbered = flags.Contains('n', StringComparison.Ordinal);
        var re = pattern is null ? null : BuildRegex(flags, pattern);
        var other = new List<string>();
        if (Hits(output, numbered, other) is not { Count: > 0 } files)
        {
            return null;
        }

        var result = new List<string>();
        var used = 0;
        foreach (var (path, hits) in files)
        {
            if (used >= Budget)
            {
                result.Add(AnchorLine(path, hits));
                continue;
            }

            var before = result.Count;
            var shown = hits.GetRange(0, Math.Min(hits.Count, PerFile));
            var indent = CommonIndent(shown);
            var body = shown.ConvertAll(h =>
            {
                var text = Cut(h.Text[Math.Min(indent, h.Text.Length)..], re);
                return h.Num is { } n ? $"{n}:{text}" : text;
            });
            if (hits.Count == 1)
            {
                result.Add($"{path}:{body[0]}");
            }
            else
            {
                result.Add(path);
                result.AddRange(body);
                if (hits.Count > shown.Count)
                {
                    result.Add($"… +{hits.Count - shown.Count} more");
                }
            }

            used += result.Count - before;
        }

        result.AddRange(other);
        var joined = string.Join('\n', result);
        return Text.Bytes(joined) < Text.Bytes(output.TrimEnd()) ? joined : null;
    }

    // The hits by path; `Binary file` notes and `grep:`/`rg:` errors go to `other`. Null when a
    // line is not `path:[num:]text`.
    private static SortedDictionary<string, List<(string? Num, string Text)>>? Hits(
        string output,
        bool numbered,
        List<string> other
    )
    {
        var files = new SortedDictionary<string, List<(string? Num, string Text)>>(
            StringComparer.Ordinal
        );
        foreach (var line in Text.Lines(output))
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (
                line.StartsWith("Binary file ", StringComparison.Ordinal)
                || line.StartsWith("grep: ", StringComparison.Ordinal)
                || line.StartsWith("rg: ", StringComparison.Ordinal)
            )
            {
                other.Add(line);
                continue;
            }

            if (
                !Text.SplitOnce(line, ":", out var path, out var rest)
                || path.Length == 0
                || path.Any(char.IsWhiteSpace)
            )
            {
                return null;
            }

            string? num = null;
            var text = rest;
            if (numbered)
            {
                if (
                    !Text.SplitOnce(rest, ":", out var n, out text)
                    || n.Length == 0
                    || !n.All(char.IsAsciiDigit)
                )
                {
                    return null;
                }

                num = n;
            }

            if (!files.TryGetValue(path, out var hits))
            {
                files[path] = hits = [];
            }

            hits.Add((num, text));
        }

        return files;
    }

    // `path:12,40,41`: where the hits are, without their text.
    private static string AnchorLine(string path, List<(string? Num, string Text)> hits)
    {
        var nums = hits.Select(h => h.Num).OfType<string>().ToList();
        if (nums.Count == 0)
        {
            return $"{path} ×{hits.Count}";
        }

        var list = string.Join(',', nums.Take(Anchors));
        if (nums.Count > Anchors)
        {
            list += $",…+{nums.Count - Anchors}";
        }

        return $"{path}:{list}";
    }

    // The flag letters (all short flags seen, `i`, `F`, `E`, `n` …) and the pattern. Null
    // when a flag changes the output shape or a flag's value is missing.
    private static (string Flags, string? Pattern)? ParseArgv(IReadOnlyList<string> argv)
    {
        if (argv.Count == 0)
        {
            return null;
        }

        var at = 1;
        if (Names.CmdStem(argv[0]) is "git")
        {
            if (argv.Count < 2)
            {
                return null;
            }

            at = 2; // `grep`
        }

        var flags = new StringBuilder();
        var patterns = new List<string>();
        string? positional = null;
        while (at < argv.Count)
        {
            var a = argv[at++];
            if (ShapeFlags.Contains(a))
            {
                return null;
            }

            if (Text.StripPrefix(a, "--") is { } flag)
            {
                if (!LongFlag(flag, argv, ref at, flags, patterns))
                {
                    return null;
                }

                continue;
            }

            if (a.Length > 1 && a[0] == '-')
            {
                if (!ShortFlags(a, argv, ref at, flags, patterns))
                {
                    return null;
                }

                continue;
            }

            positional ??= a;
        }

        return (flags.ToString(), patterns.Count == 0 ? positional : string.Join('|', patterns));
    }

    // One `--flag`: its letter goes to `flags`, a `--regexp` to `patterns`, a value-taking
    // flag skips its value. False when `--regexp` has no value.
    private static bool LongFlag(
        string flag,
        IReadOnlyList<string> argv,
        ref int at,
        StringBuilder flags,
        List<string> patterns
    )
    {
        switch (flag)
        {
            case "line-number":
                flags.Append('n');
                break;
            case "ignore-case":
                flags.Append('i');
                break;
            case "fixed-strings":
                flags.Append('F');
                break;
            case "extended-regexp":
                flags.Append('E');
                break;
            case "perl-regexp":
                flags.Append('P');
                break;
            case var l when ValueLong.Contains(l):
                at++;
                break;
            case "regexp":
                if (at >= argv.Count)
                {
                    return false;
                }

                patterns.Add(argv[at++]);
                break;
            case var l when l.StartsWith("regexp=", StringComparison.Ordinal):
                patterns.Add(l["regexp=".Length..]);
                break;
        }

        return true;
    }

    // A `-abc` cluster: letters go to `flags` until `-e` (the rest or the next word is a
    // pattern) or a value-taking letter. False on a shape flag or a missing `-e` value.
    private static bool ShortFlags(
        string a,
        IReadOnlyList<string> argv,
        ref int at,
        StringBuilder flags,
        List<string> patterns
    )
    {
        for (var k = 1; k < a.Length; k++)
        {
            var c = a[k];
            if (ShapeFlags.Contains($"-{c}"))
            {
                return false;
            }

            if (c == 'e')
            {
                var rest = a[(k + 1)..];
                if (rest.Length == 0)
                {
                    if (at >= argv.Count)
                    {
                        return false;
                    }

                    rest = argv[at++];
                }

                patterns.Add(rest);
                break;
            }

            if (ValueShort.Contains(c, StringComparison.Ordinal))
            {
                if (k + 1 == a.Length)
                {
                    at++;
                }

                break;
            }

            flags.Append(c);
        }

        return true;
    }

    // The pattern as a regex, or null when its dialect may not read the same way here (a BRE
    // with `\|`, a `-P` lookaround): no window is cut then.
    private static Regex? BuildRegex(string flags, string pattern)
    {
        string p;
        if (flags.Contains('F', StringComparison.Ordinal))
        {
            p = Regex.Escape(pattern);
        }
        else if (
            pattern.Contains('\\', StringComparison.Ordinal)
            && !flags.Contains('E', StringComparison.Ordinal)
            && !flags.Contains('P', StringComparison.Ordinal)
        )
        {
            return null;
        }
        else
        {
            p = pattern;
        }

        var options = RegexOptions.CultureInvariant;
        if (flags.Contains('i', StringComparison.Ordinal))
        {
            options |= RegexOptions.IgnoreCase;
        }

        try
        {
            return new Regex(p, options, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Chars of leading whitespace every shown hit shares; relative indentation stays.
    private static int CommonIndent(List<(string? Num, string Text)> hits)
    {
        var indents = hits.Where(h => !string.IsNullOrWhiteSpace(h.Text))
            .Select(h => h.Text.TakeWhile(c => c is ' ' or '\t').Count())
            .ToList();
        return indents.Count == 0 ? 0 : indents.Min();
    }

    // `text` cut to `Window` chars either side of the first hit; whole when it is short or the
    // pattern is unknown or not found in it.
    private static string Cut(string text, Regex? re)
    {
        Match m;
        try
        {
            if (re is null || !(m = re.Match(text)).Success)
            {
                return text;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }

        var start = BackScalars(text, m.Index, Window);
        var end = ForwardScalars(text, m.Index + m.Length, Window);
        if (start == 0 && end == text.Length)
        {
            return text;
        }

        return $"{(start > 0 ? "…" : "")}{text[start..end]}{(end < text.Length ? "…" : "")}";
    }

    // The index `n` scalars before `from`, or 0 when fewer are there.
    private static int BackScalars(string text, int from, int n)
    {
        var i = from;
        for (var k = 0; k < n; k++)
        {
            if (i == 0)
            {
                return 0;
            }

            i -= i >= 2 && char.IsSurrogatePair(text[i - 2], text[i - 1]) ? 2 : 1;
        }

        return i;
    }

    // The index `n` scalars after `from`, or the end when fewer are there.
    private static int ForwardScalars(string text, int from, int n)
    {
        var i = from;
        for (var k = 0; k < n && i < text.Length; k++)
        {
            i += i + 1 < text.Length && char.IsSurrogatePair(text[i], text[i + 1]) ? 2 : 1;
        }

        return i;
    }
}
