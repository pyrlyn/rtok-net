using System.Globalization;
using System.Text.RegularExpressions;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// <c>git diff</c> and <c>git show</c> in two tiers (rtok T681). A diff that fits the budget keeps
/// every changed line and one line of context around it; longer unchanged runs fold to
/// <c>⋯ N unchanged</c>. A bigger one becomes a <c>path +A -D</c> line per file with its hunk
/// headers, and only the smallest files keep their hunks. Every file path is in the output either
/// way. Hunks are read by the line counts in their <c>@@</c> header, so a deleted line starting
/// with <c>--</c> is never taken for a file header. Anything that is not a plain
/// <c>diff --git</c> stream returns null and the caller's older filter applies.
/// </summary>
internal static partial class Diffs
{
    // Raw diffs up to this many lines stay whole (tier one).
    private const int Small = 120;

    // Tier two shows the hunks of at most this many files...
    private const int DetailFiles = 3;

    // ...and of those only while their folded bodies fit this many bytes together.
    private const int DetailBytes = 2500;

    // The function text git appends to a hunk header is cut at this many characters.
    private const int HeadContext = 60;

    /// <summary><paramref name="output"/> as a tiered diff, or null when it is not a plain
    /// <c>diff --git</c> stream or the result would not be shorter.</summary>
    public static string? Tiered(string output)
    {
        var lines = Text.Lines(output);
        if (Parse(lines) is not { } parsed)
        {
            return null;
        }

        var result = new List<string>(parsed.Preamble);
        if (lines.Count <= Small)
        {
            foreach (var f in parsed.Files)
            {
                result.AddRange(Whole(f));
            }
        }
        else
        {
            result.AddRange(TierTwo(parsed));
        }

        var got = string.Join('\n', result);
        return Text.Bytes(got) < Text.Bytes(output) ? got : null;
    }

    private static (int Old, int New)? Counts(string head)
    {
        var m = HunkHead().Match(head);
        if (!m.Success)
        {
            return null;
        }

        int? N(string group) =>
            !m.Groups[group].Success ? 1
            : int.TryParse(m.Groups[group].ValueSpan, CultureInfo.InvariantCulture, out var n) ? n
            : null;
        return N("old") is { } old && N("new") is { } @new ? (old, @new) : null;
    }

    private static Parsed? Parse(List<string> lines)
    {
        var at = lines.FindIndex(l => l.StartsWith("diff --git ", StringComparison.Ordinal));
        if (at < 0)
        {
            return null;
        }

        var parsed = new Parsed(lines[..at], []);
        while (at < lines.Count)
        {
            if (!lines[at].StartsWith("diff --git ", StringComparison.Ordinal))
            {
                return null;
            }

            var file = new DiffFile([lines[at]], []);
            at++;
            while (
                at < lines.Count
                && !lines[at].StartsWith("@@ ", StringComparison.Ordinal)
                && !lines[at].StartsWith("diff --git ", StringComparison.Ordinal)
            )
            {
                file.Header.Add(lines[at++]);
            }

            while (at < lines.Count && lines[at].StartsWith("@@ ", StringComparison.Ordinal))
            {
                var head = lines[at];
                if (Counts(head) is not var (old, @new))
                {
                    return null;
                }

                at++;
                if (HunkBody(lines, ref at, old, @new) is not { } body)
                {
                    return null;
                }

                file.Hunks.Add(new Hunk(head, body));
            }

            parsed.Files.Add(file);
        }

        return parsed;
    }

    // A hunk's lines from `at` until its `old`/`new` counts run out, plus any
    // `\ No newline at end of file` marks; null when a line breaks the counts.
    private static List<string>? HunkBody(List<string> lines, ref int at, int old, int @new)
    {
        var body = new List<string>();
        while (old > 0 || @new > 0)
        {
            if (at >= lines.Count)
            {
                return null;
            }

            var line = lines[at];
            var sign = line.Length > 0 ? line[0] : '\0';
            if (sign is ' ' || line.Length == 0)
            {
                if (old == 0 || @new == 0)
                {
                    return null;
                }

                (old, @new) = (old - 1, @new - 1);
            }
            else if (sign is '-')
            {
                if (old-- == 0)
                {
                    return null;
                }
            }
            else if (sign is '+')
            {
                if (@new-- == 0)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            body.Add(line);
            at++;
        }

        while (at < lines.Count && lines[at].StartsWith('\\'))
        {
            body.Add(lines[at++]);
        }

        return body;
    }

    private static int Changed(Hunk h, char sign) => h.Body.Count(l => l.StartsWith(sign));

    // The path a file's header names: the `b/` side of the `diff --git` line, `old -> new`
    // for a rename.
    private static string PathOf(DiffFile file)
    {
        var first = Text.TrimStartMatches(file.Header[0], "diff --git ");
        var split = first.LastIndexOf(" b/", StringComparison.Ordinal);
        if (split < 0)
        {
            return first;
        }

        var old = Text.TrimStartMatches(first[..split], "a/");
        var @new = first[(split + 3)..];
        return string.Equals(old, @new, StringComparison.Ordinal) ? @new : $"{old} -> {@new}";
    }

    // What the header says besides the path: new, deleted, renamed, binary, mode change.
    private static string NoteOf(DiffFile file)
    {
        bool Has(string p) => file.Header.Exists(l => l.StartsWith(p, StringComparison.Ordinal));
        var notes = new List<string>();
        if (Has("new file mode"))
        {
            notes.Add("new");
        }

        if (Has("deleted file mode"))
        {
            notes.Add("deleted");
        }

        if (Has("Binary files") || Has("GIT binary patch"))
        {
            notes.Add("binary");
        }

        if (Has("old mode"))
        {
            notes.Add("mode");
        }

        return notes.Count == 0 ? "" : $" ({string.Join(", ", notes)})";
    }

    // Header lines worth keeping: `index`, the `diff --git` line (when `+++` names the file)
    // and a `--- a/x` matching `+++ b/x` are bookkeeping.
    private static List<string> HeaderLines(DiffFile file)
    {
        var named = file.Header.Exists(l => l.StartsWith("+++ ", StringComparison.Ordinal));
        var result = new List<string>();
        for (var i = 0; i < file.Header.Count; i++)
        {
            var l = file.Header[i];
            if (l.StartsWith("index ", StringComparison.Ordinal) || (i == 0 && named))
            {
                continue;
            }

            if (
                Text.StripPrefix(l, "--- a/") is { } a
                && i + 1 < file.Header.Count
                && string.Equals(
                    Text.StripPrefix(file.Header[i + 1], "+++ b/"),
                    a,
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            result.Add(l);
        }

        return result;
    }

    // A hunk body with one line of context around each change; longer runs fold.
    private static List<string> Fold(List<string> body)
    {
        static bool IsCtx(string l) => l.Length == 0 || l[0] == ' ';
        var first = body.FindIndex(l => !IsCtx(l));
        var last = body.FindLastIndex(l => !IsCtx(l));
        if (first < 0)
        {
            return [.. body];
        }

        var result = new List<string>();
        var i = 0;
        while (i < body.Count)
        {
            if (!IsCtx(body[i]))
            {
                result.Add(body[i++]);
                continue;
            }

            var j = i;
            while (j < body.Count && IsCtx(body[j]))
            {
                j++;
            }

            var (head, tail) =
                i < first ? (0, 1)
                : j > last ? (1, 0)
                : (1, 1);
            var run = j - i;
            var hidden = Math.Max(run - (head + tail), 0);
            if (hidden >= 2)
            {
                result.AddRange(body.GetRange(i, head));
                result.Add($" ⋯ {hidden} unchanged");
                result.AddRange(body.GetRange(j - tail, tail));
            }
            else
            {
                result.AddRange(body.GetRange(i, run));
            }

            i = j;
        }

        return result;
    }

    // The hunk header with git's trailing function text cut short.
    private static string Head(Hunk h)
    {
        var close = h.Head.IndexOf("@@", 2, StringComparison.Ordinal);
        if (close < 0)
        {
            return h.Head;
        }

        var end = close + 2;
        var context = h.Head[end..];
        return Text.Scalars(context) > HeadContext
            ? $"{h.Head[..end]}{Text.TakeScalars(context, HeadContext)}…"
            : h.Head;
    }

    private static List<string> Whole(DiffFile file)
    {
        var result = HeaderLines(file);
        foreach (var h in file.Hunks)
        {
            result.Add(Head(h));
            result.AddRange(Fold(h.Body));
        }

        return result;
    }

    private static List<string> TierTwo(Parsed parsed)
    {
        var sizes = parsed.Files.ConvertAll(f => Whole(f).Sum(l => Text.Bytes(l) + 1));
        var order = Enumerable.Range(0, parsed.Files.Count).OrderBy(i => sizes[i]);
        var detail = new bool[parsed.Files.Count];
        var used = 0;
        foreach (var i in order.Take(DetailFiles))
        {
            if (parsed.Files[i].Hunks.Count == 0)
            {
                continue;
            }

            if (used + sizes[i] > DetailBytes)
            {
                break;
            }

            used += sizes[i];
            detail[i] = true;
        }

        var add = parsed.Files.Sum(f => f.Hunks.Sum(h => Changed(h, '+')));
        var del = parsed.Files.Sum(f => f.Hunks.Sum(h => Changed(h, '-')));
        var result = new List<string> { $"{parsed.Files.Count} files, +{add} -{del}" };
        for (var i = 0; i < parsed.Files.Count; i++)
        {
            var f = parsed.Files[i];
            var (a, d) = (f.Hunks.Sum(h => Changed(h, '+')), f.Hunks.Sum(h => Changed(h, '-')));
            result.Add($"{PathOf(f)} +{a} -{d}{NoteOf(f)}");
            if (detail[i])
            {
                result.AddRange(
                    Whole(f)
                        .Where(l =>
                            !l.StartsWith("+++ ", StringComparison.Ordinal)
                            && !l.StartsWith("--- ", StringComparison.Ordinal)
                        )
                );
            }
            else
            {
                result.AddRange(f.Hunks.Select(h => $"  {Head(h)}"));
            }
        }

        return result;
    }

    [GeneratedRegex(
        @"^@@ -\d+(?:,(?<old>\d+))? \+\d+(?:,(?<new>\d+))? @@",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex HunkHead();

    private sealed record Hunk(string Head, List<string> Body);

    private sealed record DiffFile(List<string> Header, List<Hunk> Hunks);

    private sealed record Parsed(List<string> Preamble, List<DiffFile> Files);
}
