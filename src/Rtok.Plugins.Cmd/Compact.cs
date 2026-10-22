using System.Globalization;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// Structured formatters for <c>cargo test</c>, <c>cargo build</c>, <c>git status</c>,
/// <c>git diff</c> and <c>ls -l</c> (rtok T661): per-suite <c>test result:</c> lines summed into
/// one, each failure's panic body kept, cargo progress dropped, <c>git status</c> as porcelain
/// codes, <c>ls -l</c> as names with sizes. Every function returns null when the input is not the
/// shape it knows, so the caller falls back instead of guessing.
/// </summary>
internal static class Compact
{
    // Lines cargo prints while it works; none carries a diagnostic.
    private static readonly string[] CargoProgress =
    [
        "Compiling ",
        "Checking ",
        "Downloading ",
        "Downloaded ",
        "Fresh ",
        "Blocking ",
        "Locking ",
        "Updating ",
        "Adding ",
        "Running ",
        "Doc-tests ",
    ];

    private static readonly (string Verb, char Code)[] StatusVerbs =
    [
        ("modified:", 'M'),
        ("new file:", 'A'),
        ("deleted:", 'D'),
        ("renamed:", 'R'),
        ("copied:", 'C'),
        ("typechange:", 'T'),
    ];

    private static readonly (string Verb, string Code)[] Unmerged =
    [
        ("both modified:", "UU"),
        ("both added:", "AA"),
        ("both deleted:", "DD"),
        ("added by us:", "AU"),
        ("added by them:", "UA"),
        ("deleted by us:", "DU"),
        ("deleted by them:", "UD"),
    ];

    private static readonly string[] StateLines =
    [
        "On branch ",
        "Your branch ",
        "HEAD detached ",
        "nothing to commit",
        "You have unmerged paths",
        "All conflicts fixed",
        "You are currently ",
        "interactive rebase in progress",
        "Last command",
        "Next command",
        "No commits yet",
        "and have ",
    ];

    private static readonly string[] Units = ["K", "M", "G", "T"];

    // More regular files than this in one listing are grouped by extension (rtok T679).
    private const int GroupAbove = 30;

    private enum Section
    {
        None,
        Staged,
        Unstaged,
        Untracked,
        Unmerged,
    }

    /// <summary>
    /// <c>cargo build</c> / <c>check</c> / <c>clippy</c>: the diagnostic headline and its
    /// <c>--&gt;</c> location, the <c>could not compile</c> verdict, and a one-word
    /// <c>Finished &lt;profile&gt;</c> on success. Two or more warnings of one lint fold into
    /// <c>lint ×N: first headline (loc, loc, …)</c> (rtok T684): every location stays.
    /// </summary>
    public static string? CargoBuild(string output)
    {
        var (pieces, warns) = BuildPieces(output);
        return pieces.Count == 0 ? null : string.Join('\n', FoldWarnings(pieces, warns));
    }

    // The build log's kept lines in order; a `warning:` block is a slot (`Line` null) that
    // points into the returned warnings.
    private static (List<(string? Line, int Warn)> Pieces, List<Warn> Warns) BuildPieces(
        string output
    )
    {
        var pieces = new List<(string? Line, int Warn)>();
        var warns = new List<Warn>();
        var open = false;
        foreach (var line in Text.Lines(output))
        {
            if (IsProgress(line) || IsGeneratedCount(line))
            {
                continue;
            }

            if (ShortFinished(line) is { } finished)
            {
                open = false;
                pieces.Add((finished, -1));
            }
            else if (line.StartsWith("warning:", StringComparison.Ordinal))
            {
                open = true;
                warns.Add(new Warn(line));
                pieces.Add((null, warns.Count - 1));
            }
            else if (open && line.TrimStart().StartsWith("-->", StringComparison.Ordinal))
            {
                warns[^1].Locs.Add(line);
            }
            else if (open && LintCode(line) is { } code)
            {
                warns[^1].Code ??= code;
            }
            else if (
                line.Contains("error[", StringComparison.Ordinal)
                || line.Contains("error:", StringComparison.Ordinal)
                || line.Contains("-->", StringComparison.Ordinal)
                || line.Contains("warning:", StringComparison.Ordinal)
            )
            {
                open = false;
                pieces.Add((line, -1));
            }
        }

        return (pieces, warns);
    }

    // Each warning slot as its block, or, for two or more warnings of one lint, one
    // `lint ×N: headline (loc, …)` line in the first one's place.
    private static List<string> FoldWarnings(
        List<(string? Line, int Warn)> pieces,
        List<Warn> warns
    )
    {
        // A warning without its lint code takes the code of another with the same headline.
        var byShape = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var w in warns)
        {
            if (w.Code is { } c)
            {
                byShape.TryAdd(HeadShape(w.Head), c);
            }
        }

        var lints = warns.ConvertAll(w => w.Code ?? byShape.GetValueOrDefault(HeadShape(w.Head)));
        string Key(int i) => lints[i] ?? HeadShape(warns[i].Head);
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < warns.Count; i++)
        {
            var key = Key(i);
            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
            }

            members.Add(i);
        }

        var kept = new List<string>();
        foreach (var (text, i) in pieces)
        {
            if (text is not null)
            {
                kept.Add(text);
                continue;
            }

            var members = groups[Key(i)];
            if (members.Count < 2)
            {
                kept.Add(warns[i].Head);
                kept.AddRange(warns[i].Locs);
            }
            else if (members[0] == i)
            {
                var locs = members
                    .SelectMany(m => warns[m].Locs)
                    .Select(l => Text.StripPrefix(l.TrimStart(), "-->"))
                    .OfType<string>()
                    .Select(l => l.Trim());
                var head = Text.TrimStartMatches(warns[i].Head, "warning: ");
                var label = lints[i] is { } lint
                    ? $"{lint} ×{members.Count}: {head}"
                    : $"{head} ×{members.Count}";
                kept.Add($"{label} ({string.Join(", ", locs)})");
            }
        }

        return kept;
    }

    /// <summary>
    /// <c>cargo test</c>: one totals line for every suite, each failure's panic body and the
    /// names that failed. A build that never reached the tests is a build log, so it goes to
    /// <see cref="CargoBuild"/>.
    /// </summary>
    public static string? CargoTest(string output)
    {
        var totals = new Totals();
        var failed = new List<string>();
        var bodies = new List<string>();
        var inFailures = false;
        var inBody = false;
        var lines = Text.Lines(output);
        foreach (var line in lines)
        {
            if (totals.Add(line))
            {
                inFailures = false;
                inBody = false;
                continue;
            }

            if (
                Text.StripPrefix(line, "test ") is { } test
                && test.EndsWith(" ... FAILED", StringComparison.Ordinal)
            )
            {
                failed.Add(test[..^" ... FAILED".Length]);
                continue;
            }

            if (line is "failures:")
            {
                // The first `failures:` opens the bodies, the second lists names we have.
                inBody = !inFailures;
                inFailures = true;
                continue;
            }

            if (
                inFailures
                && inBody
                && !line.StartsWith("---- ", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(line)
                && !line.StartsWith("note: run with `RUST_BACKTRACE", StringComparison.Ordinal)
            )
            {
                bodies.Add(line);
            }
        }

        if (totals.Suites == 0)
        {
            var compile = lines.Exists(l =>
                l.StartsWith("error[", StringComparison.Ordinal)
                || l.StartsWith("error:", StringComparison.Ordinal)
            );
            return compile ? CargoBuild(output) : null;
        }

        return TestSummary(lines, totals, failed, bodies);
    }

    // The panic bodies, the failed names, the totals and cargo's own verdict
    // (`error: test failed, to rerun pass -p x --lib`), which names what to run again and
    // comes last, as cargo prints it.
    private static string TestSummary(
        List<string> lines,
        Totals totals,
        List<string> failed,
        List<string> bodies
    )
    {
        var verdicts = lines.FindAll(l =>
            l.StartsWith("error:", StringComparison.Ordinal) && !bodies.Contains(l)
        );
        var result = new List<string>(bodies);
        if (failed.Count > 0)
        {
            result.Add($"FAILED: {string.Join(", ", failed)}");
        }

        result.Add(totals.Line());
        result.AddRange(verdicts);
        return string.Join('\n', result);
    }

    /// <summary>
    /// <c>git status</c> long format as porcelain <c>XY path</c>: staged changes in the first
    /// column, worktree changes in the second, <c>??</c> for untracked. Hints and section prose go;
    /// the branch lines and <c>nothing to commit</c> stay.
    /// </summary>
    public static string? GitStatus(string output)
    {
        var result = new List<StatusLine>();
        var section = Section.None;
        var entries = 0;
        foreach (var line in Text.Lines(output))
        {
            var head = line.Trim();
            if (head.Length == 0 || (head.StartsWith('(') && head.EndsWith(')')))
            {
                continue;
            }

            if (SectionOf(head) is { } opened)
            {
                section = opened;
            }
            else if (Restates(head))
            {
                // Restates the sections above.
            }
            else if (line.StartsWith('\t') || line.StartsWith(' '))
            {
                if (section == Section.None)
                {
                    return null;
                }

                var code = EntryCode(section, head);
                if (code is null)
                {
                    return null;
                }

                var path =
                    section != Section.Untracked && Text.SplitOnce(head, ":", out _, out var after)
                        ? after.Trim()
                        : head;
                result.Add(new StatusLine(null, code, path));
                entries++;
            }
            else if (Array.Exists(StateLines, p => head.StartsWith(p, StringComparison.Ordinal)))
            {
                result.Add(new StatusLine(head, null, null));
            }

            // Any other top-level line (an echoed command, a wrapper banner) is dropped, as
            // the long-format filter this replaces did.
        }

        return result.Count == 0 || (entries == 0 && section != Section.None)
            ? null
            : RenderStatus(result);
    }

    /// <summary>
    /// <c>git diff</c>: hunks verbatim; per file only the <c>+++ b/path</c> header. <c>index</c>
    /// lines, the <c>diff --git</c> line and a <c>--- a/path</c> naming the same file are
    /// bookkeeping. Mode, rename, binary and <c>/dev/null</c> lines stay.
    /// </summary>
    public static string GitDiff(string output)
    {
        var lines = Text.Lines(output);
        var result = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l.StartsWith("index ", StringComparison.Ordinal))
            {
                continue;
            }

            // Kept only when no `+++` header follows before the next file (a binary or
            // mode-only change): then it is the only line naming the file.
            if (l.StartsWith("diff --git ", StringComparison.Ordinal) && HasHeader(lines, i + 1))
            {
                continue;
            }

            if (
                Text.StripPrefix(l, "--- a/") is { } a
                && i + 1 < lines.Count
                && Text.StripPrefix(lines[i + 1], "+++ b/") is { } b
                && string.Equals(a, b, StringComparison.Ordinal)
            )
            {
                continue;
            }

            result.Add(l);
        }

        return string.Join('\n', result);
    }

    /// <summary>True when an <c>ls</c> argv asks for the long format (<c>-l</c>, <c>-la</c>,
    /// <c>-lh</c> …).</summary>
    public static bool LsLong(IReadOnlyList<string> argv) =>
        argv.Skip(1)
            .Any(a =>
                a.StartsWith('-')
                && !a.StartsWith("--", StringComparison.Ordinal)
                && a[1..].Contains('l', StringComparison.Ordinal)
            );

    /// <summary>
    /// <c>ls -l</c>: directories on one line, then one <c>name size</c> per file; <c>.</c>/<c>..</c>,
    /// <c>total</c>, owner, group, link count and dates go. Symlinks keep <c>-&gt; target</c>,
    /// executables get <c>*</c>. Any line not in the GNU/BSD long shape returns null.
    /// </summary>
    public static string? LsL(string output)
    {
        var dirs = new List<string>();
        var files = new List<LsEntry>();
        var groups = new List<string>();
        void Flush()
        {
            var g = new List<string>();
            if (dirs.Count > 0)
            {
                g.Add($"dirs: {string.Join(' ', dirs)}");
            }

            g.AddRange(RenderFiles(files));
            files = [];
            dirs.Clear();
            groups.Add(string.Join('\n', g));
        }

        foreach (var line in Text.Lines(output))
        {
            if (line.Length == 0 || line.StartsWith("total ", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.EndsWith(':') && !line.Contains(' ', StringComparison.Ordinal))
            {
                // `ls -l a b` prints `a:` headers.
                if (dirs.Count > 0 || files.Count > 0)
                {
                    Flush();
                }

                groups.Add(line);
                continue;
            }

            if (!TryLsLine(line, out var dir, out var file))
            {
                return null;
            }

            if (dir is not null)
            {
                dirs.Add(dir);
            }
            else if (file is { } f)
            {
                files.Add(f);
            }
        }

        if (dirs.Count > 0 || files.Count > 0)
        {
            Flush();
        }

        return groups.Count == 0 ? null : string.Join('\n', groups);
    }

    // One `ls -l` entry: a `name/` directory or a file; neither for `.` and `..`. False when
    // the line is not in the GNU/BSD long shape.
    private static bool TryLsLine(string line, out string? dir, out LsEntry? file)
    {
        (dir, file) = (null, null);
        var fields = Text.Words(line);
        if (fields.Length < 8)
        {
            return false;
        }

        var perms = fields[0];
        if (Text.Bytes(perms) < 10 || !"-dlcbps".Contains(perms[0], StringComparison.Ordinal))
        {
            return false;
        }

        // Device files print `major, minor`; keep the shape simple and stand down.
        if (ParseCount(fields[4]) is not { } size)
        {
            return false;
        }

        // The name is everything after the 8th field, spaces included.
        if (NthFieldRest(line, 8) is not { } name)
        {
            return false;
        }

        if (name is "." or "..")
        {
            return true;
        }

        switch (perms[0])
        {
            case 'd':
                dir = $"{name}/";
                break;
            case 'l':
                file = new LsEntry(name, false, 0, true);
                break;
            default:
                file = new LsEntry(
                    name,
                    perms[1..].Contains('x', StringComparison.Ordinal),
                    size,
                    false
                );
                break;
        }

        return true;
    }

    /// <summary>A decimal count as Rust's <c>str::parse::&lt;u64&gt;</c> reads it.</summary>
    internal static ulong? ParseCount(string text) =>
        ulong.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    private static bool IsProgress(string line)
    {
        var t = line.TrimStart();
        return Array.Exists(CargoProgress, p => t.StartsWith(p, StringComparison.Ordinal));
    }

    // `Finished `dev` profile [unoptimized + debuginfo] target(s) in 3.2s` → `Finished dev`.
    private static string? ShortFinished(string line)
    {
        if (Text.StripPrefix(line.TrimStart(), "Finished ") is not { } t)
        {
            return null;
        }

        var parts = t.Split('`');
        var profile = parts.Length > 1 ? parts[1].Trim() : "";
        return profile.Length == 0 ? "Finished" : $"Finished {profile}";
    }

    // `warning: `foo` (lib) generated 2 warnings` repeats the count of the blocks above it.
    private static bool IsGeneratedCount(string line) =>
        line.StartsWith("warning: `", StringComparison.Ordinal)
        && line.Contains(" generated ", StringComparison.Ordinal)
        && line.Contains("warning", StringComparison.Ordinal);

    // `#[warn(unused_imports)]` or a clippy `…/index.html#redundant_clone` link → the lint name.
    private static string? LintCode(string line)
    {
        var warn = line.Split("#[warn(");
        if (warn.Length > 1)
        {
            return warn[1].Split(')')[0];
        }

        var clippy = line.Split("rust-clippy/");
        if (clippy.Length < 2)
        {
            return null;
        }

        var anchor = clippy[1].Split("index.html#");
        if (anchor.Length < 2 || Text.Words(anchor[1]) is not [var name, ..])
        {
            return null;
        }

        return $"clippy::{name}";
    }

    // The headline with its backticked names removed, so `unused import: `a`` and `…: `b``
    // share one shape.
    private static string HeadShape(string head) =>
        string.Concat(head.Split('`').Where((_, i) => i % 2 == 0));

    private static Section? SectionOf(string head) =>
        head.StartsWith("Changes to be committed:", StringComparison.Ordinal) ? Section.Staged
        : head.StartsWith("Changes not staged for commit:", StringComparison.Ordinal)
            ? Section.Unstaged
        : head.StartsWith("Untracked files:", StringComparison.Ordinal) ? Section.Untracked
        : head.StartsWith("Unmerged paths:", StringComparison.Ordinal) ? Section.Unmerged
        : null;

    private static bool Restates(string head) =>
        head.StartsWith("no changes added to commit", StringComparison.Ordinal)
        || head.StartsWith("nothing added to commit but untracked", StringComparison.Ordinal);

    private static string? EntryCode(Section section, string head)
    {
        if (section == Section.Untracked)
        {
            return "??";
        }

        if (section == Section.Unmerged)
        {
            foreach (var (verb, code) in Unmerged)
            {
                if (head.StartsWith(verb, StringComparison.Ordinal))
                {
                    return code;
                }
            }

            return null;
        }

        foreach (var (verb, code) in StatusVerbs)
        {
            if (head.StartsWith(verb, StringComparison.Ordinal))
            {
                return section == Section.Staged ? $"{code} " : $" {code}";
            }
        }

        return null;
    }

    // The branch sentences join the `On branch name` line as `ahead N (origin/x)`; the entries
    // of a section share a line per XY code, with paths of one directory as `dir/{a,b}` (T680).
    private static string RenderStatus(List<StatusLine> lines)
    {
        var result = new List<string>();
        var run = new List<(string Code, string Path)>();
        int? branch = null;
        foreach (var line in lines)
        {
            if (line.Text is not { } text)
            {
                run.Add((line.Code!, line.Path!));
                continue;
            }

            result.AddRange(GroupEntries(run));
            run.Clear();
            if (Text.StripPrefix(text, "On branch ") is { } name)
            {
                branch = result.Count;
                result.Add($"On branch {name}");
            }
            else if (branch is { } at && UpstreamNote(text, result[at]) is { } note)
            {
                result[at] = note;
            }
            else
            {
                result.Add(text);
            }
        }

        result.AddRange(GroupEntries(run));
        return string.Join('\n', result);
    }

    // The branch line with `text` (a `Your branch …` sentence) folded in, or null.
    private static string? UpstreamNote(string text, string branch)
    {
        static (string Remote, string After)? Remote(string rest) =>
            Text.SplitOnce(rest, "'", out var r, out var after) ? (r, after) : null;
        static string? Count(string after) =>
            Text.SplitOnce(after, " by ", out _, out var n) && Text.Words(n) is [var first, ..]
                ? first
                : null;

        if (text.StartsWith("Your branch is up to date with '", StringComparison.Ordinal))
        {
            return branch;
        }

        if (Text.StripPrefix(text, "Your branch is ahead of '") is { } ahead)
        {
            return Remote(ahead) is var (r, after) && Count(after) is { } n
                ? $"{branch} ahead {n} ({r})"
                : null;
        }

        if (Text.StripPrefix(text, "Your branch is behind '") is { } behind)
        {
            return Remote(behind) is var (r, after) && Count(after) is { } n
                ? $"{branch} behind {n} ({r})"
                : null;
        }

        if (Text.StripPrefix(text, "Your branch and '") is { } diverged)
        {
            return Remote(diverged) is var (r, _) ? $"{branch} diverged ({r})" : null;
        }

        if (
            Text.StripPrefix(text, "and have ") is not { } counts
            || !Text.SplitOnce(counts, " and ", out var plus, out var rest)
            || Text.Words(rest) is not [var minus, ..]
            || !branch.Contains(" diverged (", StringComparison.Ordinal)
        )
        {
            return null;
        }

        var at = branch.IndexOf(" diverged", StringComparison.Ordinal);
        return $"{branch[..at]} diverged +{plus} -{minus}{branch[(at + " diverged".Length)..]}";
    }

    // One line per XY code: its paths, a directory's paths together as `dir/{a,b}`. A path
    // with a space, comma or brace keeps its own line so the grouping stays readable.
    private static List<string> GroupEntries(List<(string Code, string Path)> run)
    {
        var lines = new List<string>();
        foreach (var code in run.Select(e => e.Code).Distinct(StringComparer.Ordinal))
        {
            var paths = run.Where(e => string.Equals(e.Code, code, StringComparison.Ordinal))
                .Select(e => e.Path)
                .ToList();
            var loose = paths.FindAll(p => p.AsSpan().IndexOfAny(" ,{}") >= 0);
            var dirs = new List<(string Dir, List<string> Leaves)>();
            foreach (var path in paths.Where(p => p.AsSpan().IndexOfAny(" ,{}") < 0))
            {
                var cut = path.TrimEnd('/').LastIndexOf('/') + 1;
                var (dir, leaf) = (path[..cut], path[cut..]);
                var found = dirs.FindIndex(d =>
                    string.Equals(d.Dir, dir, StringComparison.Ordinal)
                );
                if (found >= 0)
                {
                    dirs[found].Leaves.Add(leaf);
                }
                else
                {
                    dirs.Add((dir, [leaf]));
                }
            }

            var items = dirs.ConvertAll(d =>
                d.Leaves.Count == 1 ? d.Dir + d.Leaves[0]
                : d.Dir.Length == 0 ? string.Join(' ', d.Leaves)
                : $"{d.Dir}{{{string.Join(',', d.Leaves)}}}"
            );
            if (items.Count > 0)
            {
                lines.Add($"{code} {string.Join(' ', items)}");
            }

            lines.AddRange(loose.Select(p => $"{code} {p}"));
        }

        return lines;
    }

    private static bool HasHeader(List<string> lines, int from)
    {
        for (var i = from; i < lines.Count; i++)
        {
            var n = lines[i];
            if (
                n.StartsWith("diff --git ", StringComparison.Ordinal)
                || n.StartsWith("@@", StringComparison.Ordinal)
            )
            {
                return false;
            }

            if (n.StartsWith("+++ ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // `1234` → `1.2K`; bytes under 1 KiB print as `NB`. Rounded half to even at one decimal,
    // in integers, as Rust's `{:.1}` rounds the exact quotient.
    private static string Human(ulong bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes}B";
        }

        UInt128 div = 1024;
        var u = 0;
        while (bytes >= div * 1024 && u + 1 < Units.Length)
        {
            div *= 1024;
            u++;
        }

        var tenths = (UInt128)bytes * 10;
        var (q, r) = (tenths / div, tenths % div);
        if (r * 2 > div || (r * 2 == div && q % 2 == 1))
        {
            q++;
        }

        return $"{q / 10}.{q % 10}{Units[u]}";
    }

    // A listing's files as lines: `name size` each; above `GroupAbove` files, one line per
    // extension (`*.rs (3): a 1.2K, b 300B, c 9K`; a lone file of its extension stays a plain
    // line); three or more dotfiles share a `dotfiles:` line. Symlinks keep their own line.
    private static List<string> RenderFiles(List<LsEntry> entries)
    {
        static string Show(string name, bool exec, ulong size) =>
            $"{name}{(exec ? "*" : "")} {Human(size)}";

        var count = entries.Count(e => !e.Link);
        var dots = entries.Count(e => !e.Link && e.Name.StartsWith('.'));
        var loose = new List<string>();
        var dotted = new List<string>();
        var byExt = new List<(string Ext, List<string> Members)>();
        var pending = new List<(int At, string Text)>();
        foreach (var e in entries)
        {
            if (e.Link)
            {
                loose.Add(e.Name);
                continue;
            }

            if (dots >= 3 && e.Name.StartsWith('.'))
            {
                dotted.Add(Show(e.Name, e.Exec, e.Size));
                continue;
            }

            var split = e.Name.LastIndexOf('.');
            if (split > 0 && split + 1 < e.Name.Length && count > GroupAbove)
            {
                var (stem, ext) = (e.Name[..split], e.Name[split..]);
                var at = byExt.FindIndex(x => string.Equals(x.Ext, ext, StringComparison.Ordinal));
                if (at < 0)
                {
                    byExt.Add((ext, []));
                    at = byExt.Count - 1;
                }

                byExt[at].Members.Add(Show(stem, e.Exec, e.Size));
                pending.Add((at, Show(e.Name, e.Exec, e.Size)));
            }
            else
            {
                loose.Add(Show(e.Name, e.Exec, e.Size));
            }
        }

        var lines = loose;
        if (dotted.Count > 0)
        {
            lines.Add($"dotfiles: {string.Join(", ", dotted)}");
        }

        lines.AddRange(ExtLines(byExt, pending));
        return lines;
    }

    // One `*.ext (N): …` line per extension with two or more files; a lone one keeps its line.
    private static List<string> ExtLines(
        List<(string Ext, List<string> Members)> byExt,
        List<(int At, string Text)> pending
    )
    {
        var lines = new List<string>();
        for (var at = 0; at < byExt.Count; at++)
        {
            var (ext, members) = byExt[at];
            if (members.Count < 2)
            {
                lines.AddRange(pending.Where(p => p.At == at).Select(p => p.Text));
            }
            else
            {
                lines.Add($"*{ext} ({members.Count}): {string.Join(", ", members)}");
            }
        }

        return lines;
    }

    // The rest of `line` after `n` whitespace-separated fields.
    private static string? NthFieldRest(string line, int n)
    {
        var rest = line.TrimStart();
        for (var i = 0; i < n; i++)
        {
            var end = FirstWhiteSpace(rest);
            if (end < 0)
            {
                return null;
            }

            rest = rest[end..].TrimStart();
        }

        return rest.Length == 0 ? null : rest;
    }

    private static int FirstWhiteSpace(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    // One `warning:` block: its headline, every `-->` location and the lint name.
    private sealed class Warn(string head)
    {
        public string Head { get; } = head;

        public List<string> Locs { get; } = [];

        public string? Code { get; set; }
    }

    // A `git status` line before it is written out: a kept sentence, or a path with its code.
    private sealed record StatusLine(string? Text, string? Code, string? Path);

    private sealed record LsEntry(string Name, bool Exec, ulong Size, bool Link);

    private sealed class Totals
    {
        private ulong _passed;
        private ulong _failed;
        private ulong _ignored;
        private ulong _filtered;

        public int Suites { get; private set; }

        // `test result: ok. 3 passed; 0 failed; 1 ignored; 0 measured; 2 filtered out; …`
        public bool Add(string line)
        {
            if (Text.StripPrefix(line, "test result: ") is not { } rest)
            {
                return false;
            }

            var seen = false;
            foreach (var part in rest.Split(';'))
            {
                var words = Text.Words(part);
                (string N, string What)? pick = words switch
                {
                    [.., var n, var w, "out"] => (n, w),
                    [.., var n, var w] => (n, w),
                    _ => null,
                };
                if (pick is not var (count, what) || ParseCount(count) is not { } k)
                {
                    continue;
                }

                seen = true;
                switch (what)
                {
                    case "passed":
                        _passed += k;
                        break;
                    case "failed":
                        _failed += k;
                        break;
                    case "ignored":
                        _ignored += k;
                        break;
                    case "filtered":
                        _filtered += k;
                        break;
                }
            }

            if (seen)
            {
                Suites++;
            }

            return seen;
        }

        public string Line()
        {
            var parts = new List<string> { $"{_passed} passed" };
            if (_failed > 0)
            {
                parts.Add($"{_failed} failed");
            }

            if (_ignored > 0)
            {
                parts.Add($"{_ignored} ignored");
            }

            if (_filtered > 0)
            {
                parts.Add($"{_filtered} filtered out");
            }

            return $"cargo test: {string.Join(", ", parts)} ({Suites} suite{(Suites == 1 ? "" : "s")})";
        }
    }
}
