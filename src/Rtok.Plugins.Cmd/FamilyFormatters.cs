namespace Rtok.Plugins.Cmd;

/// <summary>
/// The family formatter arms (git, cargo, grep, pytest, docker, kubectl, …) and the pre-passes
/// that clean a wrapper's or loop's output before routing (rtok T3.3). An arm that declines
/// returns null and the family's TOML rule runs instead.
/// </summary>
internal static class FamilyFormatters
{
    // Commits listed by `GitLog` before the rest is counted as `… +K more`.
    internal const int GitLogMaxCommits = 100;

    private static readonly string[] Verdicts = ["error:", "error[", "fatal:", "Error:"];

    private static readonly string[] StatusVerbs =
    [
        "modified:",
        "new file:",
        "deleted:",
        "renamed:",
        "copied:",
        "both modified:",
        "both added:",
        "both deleted:",
        "added by us:",
        "unmerged:",
    ];

    private static readonly string[] StatusLines =
    [
        "On branch ",
        "Your branch ",
        "HEAD detached ",
        "nothing to commit",
        "no changes added to commit",
        "nothing added to commit",
    ];

    /// <summary>The (bin, sub) arms <see cref="Format"/> knows; <c>*</c> is any subcommand. The
    /// golden coverage test reads this list.</summary>
    internal static readonly (string Bin, string Sub)[] Arms =
    [
        ("cargo", "test"),
        ("cargo", "build"),
        ("cargo", "clippy"),
        ("git", "status"),
        ("git", "diff"),
        ("git", "show"),
        ("ls", "*"),
        ("grep", "*"),
        ("rg", "*"),
        ("git", "grep"),
        ("git", "log"),
        ("gh", "*"),
        ("pytest", "*"),
        ("python", "*"),
        ("jest", "*"),
        ("vitest", "*"),
        ("go", "test"),
        ("wc", "*"),
        ("for", "*"),
        ("tree", "*"),
        ("docker", "ps"),
        ("podman", "ps"),
        ("kubectl", "get"),
        ("ps", "aux"),
        ("df", "*"),
        ("sqlite3", "*"),
        ("mise", "*"),
    ];

    /// <summary>
    /// The family formatter's text for <paramref name="output"/>, or null to fall back to the
    /// rule. The text is never longer than <paramref name="output"/> and keeps every ERROR/WARN
    /// line; after a failed run (<paramref name="exit"/> ≠ 0) it also keeps every stderr verdict
    /// line (<c>error:</c>, <c>fatal:</c> …). A formatter that would lose one declines, and the
    /// rule's failed-run tail keeps them.
    /// </summary>
    public static string? Format(IReadOnlyList<string> argv, string output, int exit)
    {
        if (Arm(argv, output) is not { } got || Text.Bytes(got) > Text.Bytes(output))
        {
            return null;
        }

        var kept = new HashSet<string>(Text.Lines(got), StringComparer.Ordinal);
        return Text.Lines(output)
            .Exists(l => (IsProblem(l) || (exit != 0 && IsVerdict(l))) && !kept.Contains(l))
            ? null
            : got;
    }

    // A log-level problem line (`ERROR`, `WARN`, `WARNING`): what an agent reads first.
    private static bool IsProblem(string line) =>
        line.Contains("ERROR", StringComparison.Ordinal)
        || line.Contains("WARN", StringComparison.Ordinal);

    // What a failing tool prints to stderr as its verdict: `error: could not compile`,
    // `fatal: bad revision`, `Error: …`.
    private static bool IsVerdict(string line) =>
        Array.Exists(Verdicts, v => line.StartsWith(v, StringComparison.Ordinal));

    /// <summary>Output with a wrapper's chatter (mise) or a polling loop's repeats removed, or
    /// null when <paramref name="stem"/> has no pre-pass or it changed nothing.</summary>
    public static string? Tidy(string stem, string output) =>
        stem switch
        {
            "mise" => Mise.Tidy(output),
            "for" or "while" or "until" => Loops.Collapse(output),
            _ => null,
        };

    private static string? Arm(IReadOnlyList<string> argv, string output) =>
        (Families.Bin(argv), Families.Sub(argv)) switch
        {
            ("cargo", "test") => Compact.CargoTest(output)
                ?? Keep(output, "FAILED", "test result:", "error[", "panicked"),
            ("cargo", "build" or "clippy") => Compact.CargoBuild(output)
                ?? Keep(output, "error[", "error:", "-->", "Finished", "warning:"),
            ("git", "status") => Compact.GitStatus(output) ?? GitStatus(output),
            ("git", "diff") => Diffs.Tiered(output) ?? Compact.GitDiff(GitDiff(output)),
            // `git show <rev>:<file>` prints a file, not a diff: only a diff stream is touched.
            ("git", "show") => Diffs.Tiered(output),
            ("ls", _) when Compact.LsLong(argv) => Compact.LsL(output),
            ("grep" or "rg", _) or ("git", "grep") => Grep.Group(argv, output),
            ("git", "log") => GitLog(output) ?? string.Join('\n', Text.Lines(output).Take(20)),
            ("gh", _) => Gh.Format(argv, output),
            ("pytest", _) => Python.Pytest(output)
                ?? Keep(output, "FAILED", "ERROR", "passed", "failed", "error"),
            ("python" or "python3" or "py", _) => Python.Script(output),
            ("jest" or "vitest", _) => Keep(output, "FAIL", "PASS", "Tests:", "● "),
            ("go", "test") => Keep(output, "FAIL", "PASS", "ok  ", "--- FAIL"),
            ("wc", _) => Tables.Wc(output),
            ("for" or "while" or "until", _) => Loops.Collapse(output),
            ("tree", _) => string.Join('\n', Text.Lines(output).Take(40)),
            // `podman ps` prints the same table (rtok T905).
            ("docker" or "podman", "ps") => Tables.DockerPs(output),
            ("kubectl", "get") => Tables.KubectlGet(output),
            ("ps", "aux") => Tables.PsAux(output),
            ("df", _) => Tables.Df(output),
            ("sqlite3", _) => Tables.Sqlite3(output),
            ("mise", _) => Mise.Tidy(output),
            _ => null,
        };

    /// <summary>
    /// <c>git log</c> in its default (medium) format, one line per commit: short hash, date,
    /// subject. A first line gives <c>[N commits, +A/-D]</c> (totals only when <c>--stat</c>
    /// printed them) and names the most frequent author once; the others are named per line.
    /// <c>--stat</c> lines stay under their commit. Null for any other shape (<c>--oneline</c>,
    /// <c>--format=…</c>, <c>-p</c>).
    /// </summary>
    internal static string? GitLog(string output)
    {
        if (LogCommits(output) is not { } commits)
        {
            return null;
        }

        var (main, authors) = MainAuthor(commits);
        var oneYear = commits.TrueForAll(c =>
            string.Equals(c.Year, commits[0].Year, StringComparison.Ordinal)
        );
        var result = new List<string> { LogHeader(commits, main, authors, oneYear) };
        foreach (var c in commits.Take(GitLogMaxCommits))
        {
            var date = oneYear ? c.Date : $"{c.Date} {c.Year}";
            var line = $"{c.Sha} {date} {c.Subject}";
            if (!string.Equals(c.Author, main, StringComparison.Ordinal))
            {
                line += $" — {c.Author}";
            }

            result.Add(line);
            result.AddRange(c.Stat.Select(s => $"  {s}"));
        }

        if (commits.Count > GitLogMaxCommits)
        {
            result.Add($"… +{commits.Count - GitLogMaxCommits} more");
        }

        return string.Join('\n', result);
    }

    // The commits of a medium-format log; null for any other shape.
    private static List<Commit>? LogCommits(string output)
    {
        var commits = new List<Commit>();
        foreach (var line in Text.Lines(output))
        {
            if (Text.StripPrefix(line, "commit ") is { } rest)
            {
                if (
                    Text.Words(rest) is not [var sha, ..]
                    || sha.Length < 7
                    || !sha.All(char.IsAsciiHexDigit)
                )
                {
                    return null;
                }

                commits.Add(new Commit(sha[..7]));
            }
            else if (commits.Count == 0 || !LogLine(commits[^1], line))
            {
                return null;
            }
        }

        return commits.Count == 0 || commits.Exists(c => c.Date.Length == 0) ? null : commits;
    }

    // Folds one line under a `commit` header into it; false for a patch (`-p`) line.
    private static bool LogLine(Commit c, string line)
    {
        if (Text.StripPrefix(line, "Author:") is { } author)
        {
            c.Author = author.Trim().Split(" <")[0];
        }
        else if (Text.StripPrefix(line, "Date:") is { } date)
        {
            // `Wed Oct 14 23:25:53 2026 +0300` → `Oct 14`, the year kept aside.
            if (Text.Words(date) is [_, var month, var day, _, var year, ..])
            {
                (c.Date, c.Year) = ($"{month} {day}", year);
            }
        }
        else if (line.StartsWith("Merge:", StringComparison.Ordinal) || line.Trim().Length == 0)
        {
            // Bookkeeping.
        }
        else if (Text.StripPrefix(line, "    ") is { } body)
        {
            if (c.Subject.Length == 0)
            {
                c.Subject = body.Trim();
            }
        }
        else if (
            line.StartsWith(' ')
            && (
                line.Contains(" | ", StringComparison.Ordinal)
                || line.Contains(" changed", StringComparison.Ordinal)
            )
        )
        {
            c.Stat.Add(line.Trim());
        }
        else if (
            line.StartsWith("diff --git", StringComparison.Ordinal)
            || line.StartsWith("@@", StringComparison.Ordinal)
        )
        {
            return false;
        }

        return true;
    }

    // The most frequent author (the first on a tie) and how many distinct authors there are.
    private static (string Main, int Authors) MainAuthor(List<Commit> commits)
    {
        var counts = new List<(string Author, int N)>();
        foreach (var c in commits)
        {
            var at = counts.FindIndex(a =>
                string.Equals(a.Author, c.Author, StringComparison.Ordinal)
            );
            if (at < 0)
            {
                counts.Add((c.Author, 1));
            }
            else
            {
                counts[at] = (c.Author, counts[at].N + 1);
            }
        }

        var (main, best) = ("", 0);
        foreach (var (a, n) in counts)
        {
            if (n > best)
            {
                (main, best) = (a, n);
            }
        }

        return (main, counts.Count);
    }

    // `[N commits, +A/-D] main unless named · year`: the count and the `--stat` totals lead
    // (rtok T682); the totals are claimed only when `--stat` printed them.
    private static string LogHeader(List<Commit> commits, string main, int authors, bool oneYear)
    {
        var head = new List<string>();
        if (main.Length > 0)
        {
            head.Add(authors == 1 ? main : $"{main} unless named");
        }

        if (oneYear)
        {
            head.Add(commits[0].Year);
        }

        var (added, removed, hasTotals) = (0UL, 0UL, false);
        foreach (
            var (ins, del) in commits
                .SelectMany(c => c.Stat)
                .Select(StatTotals)
                .OfType<(ulong, ulong)>()
        )
        {
            (added, removed, hasTotals) = (added + ins, removed + del, true);
        }

        var countsLine = $"[{commits.Count} commit{(commits.Count == 1 ? "" : "s")}";
        if (hasTotals)
        {
            countsLine += $", +{added}/-{removed}";
        }

        countsLine += "]";
        if (head.Count > 0)
        {
            countsLine += $" {string.Join(" · ", head)}";
        }

        return countsLine;
    }

    // `(insertions, deletions)` of a `--stat` summary line (`3 files changed, 5 insertions(+),
    // 1 deletion(-)`); null for a per-file line, whose `|` a summary never has.
    private static (ulong, ulong)? StatTotals(string line)
    {
        if (
            line.Contains(" | ", StringComparison.Ordinal)
            || !line.Contains(" changed", StringComparison.Ordinal)
        )
        {
            return null;
        }

        ulong Count(string word) =>
            line.Split(',').FirstOrDefault(part => part.Contains(word, StringComparison.Ordinal))
                is { } part
            && Text.Words(part) is [var n, ..]
            && Compact.ParseCount(n) is { } k
                ? k
                : 0;

        return (Count("insertion"), Count("deletion"));
    }

    // `git diff`: the changed lines are the answer, so only the blob-hash bookkeeping goes.
    private static string GitDiff(string output) =>
        string.Join(
            '\n',
            Text.Lines(output).Where(l => !l.StartsWith("index ", StringComparison.Ordinal))
        );

    // `git status` the long-format parser declined: the branch line, what changed, and the
    // untracked paths under their header. Porcelain codes are read on the raw line, so
    // ` M x` keeps its first column. Empty output falls back to the first 15 lines.
    private static string GitStatus(string output)
    {
        var result = new List<string>();
        var untracked = false;
        var lines = Text.Lines(output);
        foreach (var line in lines)
        {
            var head = line.TrimStart();
            if (head.StartsWith("Untracked files:", StringComparison.Ordinal))
            {
                untracked = true;
                result.Add(head);
            }
            else if (
                Array.Exists(StatusVerbs, v => head.StartsWith(v, StringComparison.Ordinal))
                || Array.Exists(StatusLines, l => head.StartsWith(l, StringComparison.Ordinal))
            )
            {
                untracked = false;
                result.Add(head);
            }
            else if (IsPorcelain(line))
            {
                untracked = false;
                result.Add(line);
            }
            else if (
                untracked
                && (line.StartsWith('\t') || line.StartsWith(' '))
                && !head.StartsWith("(use ", StringComparison.Ordinal)
            )
            {
                // An untracked path, indented under its header in the long format.
                result.Add(head);
            }
        }

        return result.Count == 0 ? string.Join('\n', lines.Take(15)) : string.Join('\n', result);
    }

    // `XY path` in the short/porcelain format: two status columns, then a space.
    private static bool IsPorcelain(string line)
    {
        static bool Column(char c) =>
            c is ' ' or 'M' or 'A' or 'D' or 'R' or 'C' or 'U' or '?' or '!';
        return line.Length > 3 && Column(line[0]) && Column(line[1]) && line[2] == ' ';
    }

    // The lines naming any needle, or the last 8 lines when none does.
    private static string Keep(string output, params string[] needles)
    {
        var lines = Text.Lines(output);
        var hits = lines.FindAll(l =>
            Array.Exists(needles, n => l.Contains(n, StringComparison.Ordinal))
        );
        return string.Join('\n', hits.Count > 0 ? hits : lines.Skip(Math.Max(lines.Count - 8, 0)));
    }

    private sealed class Commit(string sha)
    {
        public string Sha { get; } = sha;

        public string Author { get; set; } = "";

        public string Date { get; set; } = "";

        public string Year { get; set; } = "";

        public string Subject { get; set; } = "";

        public List<string> Stat { get; } = [];
    }
}
