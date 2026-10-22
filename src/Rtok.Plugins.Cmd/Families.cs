using System.Text;
using Rtok.Core;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// Which family a command belongs to and how its output is compacted (rtok T3.3): a family
/// formatter, else the family's TOML rule, else the default rule. The per-family formatter arms
/// are T18's; this is the route to them.
/// </summary>
internal static class Families
{
    // Stems with a family formatter (any subcommand); `rtok stats` labels the whole stem.
    private static readonly HashSet<string> FormatterStems = new(StringComparer.Ordinal)
    {
        "cargo",
        "git",
        "gh",
        "pytest",
        "python",
        "python3",
        "py",
        "jest",
        "vitest",
        "tree",
        "go",
        "docker",
        "podman",
        "kubectl",
        "ps",
        "df",
        "sqlite3",
        "wc",
        "mise",
        "for",
        "while",
        "until",
    };

    private static readonly HashSet<string> WrapperStems = new(StringComparer.Ordinal)
    {
        "mise",
        "just",
        "uv",
        "uvx",
        "npx",
        "pnpm",
        "yarn",
        "poetry",
        "pipenv",
        "hatch",
        "python",
        "python3",
        "py",
        "cmd",
    };

    /// <summary>
    /// Compacts <paramref name="output"/>. The kind is <c>formatter</c>, <c>rule</c>,
    /// <c>skill</c>, <c>raw</c> (a bounded passthrough) or <c>unmatched</c> (a rule or formatter
    /// ran and shrank nothing).
    /// </summary>
    public static (string Text, string Kind) Compress(
        CmdSettings settings,
        IReadOnlyList<string> argv,
        string output,
        int exit,
        string archiveId
    )
    {
        // A wrapper's chatter (mise installs) or a polling loop's repeats go before the
        // route is chosen; dropping them is that formatter's work even if nothing else shrinks.
        if (FamilyFormatters.Tidy(Bin(FamilyArgv(argv)), output) is { } clean)
        {
            var (text, kind) = CompressText(settings, argv, clean, exit, archiveId);
            return (text, kind is "raw" or "unmatched" ? "formatter" : kind);
        }

        return CompressText(settings, argv, output, exit, archiveId);
    }

    /// <summary>
    /// The argv a family is matched against. The hook quotes the whole command into one argv
    /// while a typed <c>rtok run -- git status</c> arrives split; both read as
    /// <c>["git", "status"]</c>. Only the first word of a snippet is considered.
    /// </summary>
    public static List<string> FamilyArgv(IReadOnlyList<string> argv) =>
        argv.Count == 1 ? [.. Text.Words(argv[0])] : [.. argv];

    /// <summary>argv[0]'s basename, the family a Measurement names; <c>other</c> when there is
    /// none, <c>script</c> for a wrapper around two programs.</summary>
    public static string Family(IReadOnlyList<string> argv)
    {
        if (argv.Count == 1 && Bounded.MixedChain(argv[0]))
        {
            var raw = FamilyArgv(argv);
            var rawStem = Bin(raw);
            return
                !string.Equals(Bin(VisibleArgv(raw)), rawStem, StringComparison.Ordinal)
                && IsWrapperStem(rawStem)
                ? "script"
                : Named(raw);
        }

        // CommandArgv sees through `cd && mise exec -- cargo`, where argv[0] is still `cd`.
        return Named(CommandArgv(argv));
    }

    /// <summary><see cref="Family"/> once the output is known, so a wrapper whose inner command
    /// is only on an echoed line (<c>[task] $ cargo test</c>) is named for that command.</summary>
    public static string FamilyOf(CmdSettings settings, IReadOnlyList<string> argv, string output)
    {
        var family = Family(argv);
        if (family is not ("mise" or "just"))
        {
            return family;
        }

        return Echoed(settings, family, output) switch
        {
            { Multi: true } => "script",
            { } resolved => Named(resolved.Argv),
            null => family,
        };
    }

    /// <summary>How <c>rtok stats</c> labels a Bash family: <c>formatter</c>, a named
    /// <c>rule</c>, or <c>default</c> (rtok T50.1).</summary>
    public static string FilterKind(CmdSettings settings, string stem)
    {
        if (FormatterStems.Contains(stem))
        {
            return "formatter";
        }

        return string.Equals(settings.Pick(stem).MatchCmd, stem, StringComparison.Ordinal)
            ? "rule"
            : "default";
    }

    /// <summary>
    /// Argv after the wrappers in front of the real command: <c>mise exec</c>, <c>just -c</c>, a
    /// leading <c>FOO=1</c>, and <c>uv run</c>/<c>npx</c>/<c>python -m</c>/<c>cmd /c</c>. At most
    /// four peels, so <c>uv run python -m pytest</c> lands on <c>pytest</c>.
    /// </summary>
    public static List<string> VisibleArgv(IReadOnlyList<string> argv)
    {
        var cur = argv.ToList();
        for (var n = 0; n < 4; n++)
        {
            var inner = Peel(cur);
            if (inner is null || inner.SequenceEqual(cur, StringComparer.Ordinal))
            {
                break;
            }

            cur = inner;
        }

        return cur;
    }

    /// <summary>Removes ANSI CSI sequences (<c>ESC [ … letter</c>).</summary>
    public static string StripAnsi(string s)
    {
        if (!s.Contains('\u001b', StringComparison.Ordinal))
        {
            return s;
        }

        var output = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\u001b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                i += 2;
                while (i < s.Length && !char.IsAsciiLetter(s[i]))
                {
                    i++;
                }

                continue;
            }

            output.Append(s[i]);
        }

        return output.ToString();
    }

    internal static string Bin(IReadOnlyList<string> argv) =>
        argv.Count > 0 ? Names.CmdStem(argv[0]) : "";

    internal static string Sub(IReadOnlyList<string> argv) => argv.Count > 1 ? argv[1] : "";

    private static (string Text, string Kind) CompressText(
        CmdSettings settings,
        IReadOnlyList<string> argv,
        string output,
        int exit,
        string archiveId
    )
    {
        // A bounded command already printed what was asked for (rtok T176). This stays ahead
        // of the mix check: `cargo test | tail -300` is bounded although it names two programs.
        var small = Text.Bytes(output) <= Bounded.MaxBytes;
        if (small && Bounded.IsBounded(string.Join(' ', argv)))
        {
            return (output, "raw");
        }

        // A string chaining two or more distinct programs is not one family's output: it
        // takes the [script] rule (rtok T177).
        var matched = Resolve(settings, argv, output);
        var stem = matched.Multi ? "script" : Bin(matched.Argv);
        if (!matched.Multi && small && Bounded.IsBounded(string.Join(' ', matched.Argv)))
        {
            return (output, "raw");
        }

        var rule = settings.Pick(stem);
        // JSON bodies skip table formatters so kubectl -o json / gh --json reach the compact
        // pass instead of a NAME/STATUS parser (rtok T65.2).
        if (JsonView.Parse(output) is { } json)
        {
            var compact = RuleEngine.ApplyParsed(settings, output, json, exit, rule, archiveId);
            return (compact, Text.Bytes(compact) < Text.Bytes(output) ? "rule" : "unmatched");
        }

        if (!matched.Multi && FamilyFormatters.Format(matched.Argv, output, exit) is { } formatted)
        {
            return (formatted, "formatter");
        }

        var s = RuleEngine.Apply(settings, output, exit, rule, archiveId);
        return (
            s,
            stem is "skill" ? "skill"
            : Text.Bytes(s) < Text.Bytes(output) ? "rule"
            : "unmatched"
        );
    }

    private static string Named(IReadOnlyList<string> argv) =>
        Bin(argv) is { Length: > 0 } found ? found : "other";

    private static Resolved Resolve(CmdSettings settings, IReadOnlyList<string> argv, string output)
    {
        if (argv.Count == 1 && Bounded.MixedChain(argv[0]))
        {
            return Resolved.Script;
        }

        var visible = CommandArgv(argv);
        var stem = Bin(visible);
        if (stem is "mise" or "just" && Echoed(settings, stem, output) is { } echoed)
        {
            return echoed;
        }

        return new(visible, false);
    }

    // The same inner command on every wrapper stage. Peeling `cargo build && cargo test`
    // would keep only `cargo test`'s needles, so that chain stays on FamilyArgv.
    private static List<string> CommandArgv(IReadOnlyList<string> argv)
    {
        var line = argv.Count == 1 ? argv[0] : string.Join(' ', argv);
        var all = Bounded.Stages(line).Where(s => s.Count > 0).ToList();
        var stages = all.Where(s => s[0] is not ("cd" or "export")).ToList();
        if (stages.Exists(s => Peel(s) is not null))
        {
            var peeled = stages.Select(VisibleArgv).ToList();
            if (peeled.Count > 0)
            {
                var first = peeled[0];
                var stem = Bin(first);
                if (
                    stem.Length > 0
                    && peeled.TrueForAll(p => string.Equals(Bin(p), stem, StringComparison.Ordinal))
                )
                {
                    var sub = Sub(first);
                    return peeled.TrueForAll(p =>
                        string.Equals(Sub(p), sub, StringComparison.Ordinal)
                    )
                        ? first
                        : [stem];
                }
            }
        }

        // `cd x && cargo test` is cargo's, not cd's.
        return stages.Count > 0 && stages.Count < all.Count
            ? VisibleArgv(stages[0])
            : VisibleArgv(FamilyArgv(argv));
    }

    private static bool IsWrapperStem(string stem) =>
        WrapperStems.Contains(stem) || IsEnvAssign(stem);

    private static List<string>? Peel(IReadOnlyList<string> argv) =>
        PeelMiseExec(argv)
        ?? PeelJustCommand(argv)
        ?? PeelLeadingEnv(argv)
        ?? PeelToolWrapper(argv);

    private static List<string>? PeelMiseExec(IReadOnlyList<string> argv)
    {
        if (
            argv.Count == 0
            || Names.CmdStem(argv[0]) is not "mise"
            || Wrappers.SkipMiseFlags(argv, 1) is not { } at
        )
        {
            return null;
        }

        if (at >= argv.Count || argv[at] is not ("exec" or "x"))
        {
            return null;
        }

        for (var i = at + 1; i < argv.Count; )
        {
            var tok = argv[i];
            if (tok is "--")
            {
                return i + 1 < argv.Count ? [.. argv.Skip(i + 1)] : null;
            }

            if (tok is "-c" or "--command")
            {
                if (i + 1 >= argv.Count)
                {
                    return null;
                }

                var words = Text.Words(argv[i + 1]);
                return words.Length > 0 ? [.. words] : null;
            }

            if (Wrappers.MiseFlagWidth(tok) is { } n)
            {
                if (i + n > argv.Count)
                {
                    return null;
                }

                i += n;
                continue;
            }

            if (tok.StartsWith('-'))
            {
                return null;
            }

            i++;
        }

        return null;
    }

    // Leading `FOO=1 BAR=baz cmd`: the assignments are not the program.
    private static List<string>? PeelLeadingEnv(IReadOnlyList<string> argv)
    {
        var i = 0;
        while (i < argv.Count && IsEnvAssign(argv[i]))
        {
            i++;
        }

        return i > 0 && i < argv.Count ? [.. argv.Skip(i)] : null;
    }

    private static bool IsEnvAssign(string tok)
    {
        // A quote in the value is an unclosed `FOO='x cd y`; peeling it would name `cd`.
        if (!Text.SplitOnce(tok, "=", out var key, out var value))
        {
            return false;
        }

        return value.Length > 0
            && value.AsSpan().IndexOfAny('\'', '"') < 0
            && key.Length > 0
            && (char.IsAsciiLetter(key[0]) || key[0] == '_')
            && key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }

    // `uv run`, `uvx`, `npx`, `pnpm exec|dlx`, `yarn dlx`, `poetry|pipenv|hatch run`,
    // `python|python3|py -m`, `cmd /c`. A flag we do not know stays on the wrapper.
    private static List<string>? PeelToolWrapper(IReadOnlyList<string> argv)
    {
        if (argv.Count == 0)
        {
            return null;
        }

        var second = Sub(argv);
        int? restAt = Names.CmdStem(argv[0]) switch
        {
            "uv" when second is "run" => 2,
            "uvx" or "npx" => 1,
            "pnpm" when second is "exec" or "dlx" => 2,
            "yarn" when second is "dlx" => 2,
            "poetry" or "pipenv" or "hatch" when second is "run" => 2,
            "python" or "python3" or "py" when second is "-m" => 2,
            "cmd" when argv.Count > 1 && second.Equals("/c", StringComparison.OrdinalIgnoreCase) =>
                2,
            _ => null,
        };
        return restAt is { } at ? CommandAfter(argv, at) : null;
    }

    private static List<string>? CommandAfter(IReadOnlyList<string> argv, int at)
    {
        if (at > argv.Count)
        {
            return null;
        }

        var rest = argv.Skip(at).ToList();
        if (rest.Count > 0 && rest[0] is "--")
        {
            rest.RemoveAt(0);
        }

        if (rest.Count == 0 || rest[0].StartsWith('-'))
        {
            return null;
        }

        if (rest.Count == 1 && rest[0].Any(char.IsWhiteSpace))
        {
            var words = Text.Words(rest[0]);
            return words.Length > 0 ? [.. words] : null;
        }

        return rest;
    }

    // `just -c` / `--command` names the program; a recipe name does not (its body is in the
    // justfile, so only an echoed line can name it).
    private static List<string>? PeelJustCommand(IReadOnlyList<string> argv)
    {
        if (argv.Count == 0 || Names.CmdStem(argv[0]) is not "just")
        {
            return null;
        }

        var i = 1;
        while (i < argv.Count)
        {
            var tok = argv[i];
            if (tok is "-c" or "--command")
            {
                return i + 1 < argv.Count ? [.. argv.Skip(i + 1)] : null;
            }

            if (!tok.StartsWith('-'))
            {
                return null;
            }

            var extra = Wrappers.JustFlagValues(tok);
            if (i + extra >= argv.Count)
            {
                return null;
            }

            i += 1 + extra;
        }

        return null;
    }

    // The inner command from a wrapper's own echo; absent when the run was quiet or the line
    // is the tool's output, which then stays on the wrapper (fail open).
    private static Resolved? Echoed(CmdSettings settings, string wrapper, string output)
    {
        List<string>? first = null;
        var stem = "";
        var firstSub = "";
        var subDiffers = false;
        foreach (var line in Text.Lines(output))
        {
            if (EchoCommand(settings, wrapper, line) is not { } cmd)
            {
                continue;
            }

            if (Bounded.MixedChain(cmd))
            {
                return Resolved.Script;
            }

            var visible = VisibleArgv(FamilyArgv([cmd]));
            var got = Bin(visible);
            if (got.Length == 0)
            {
                continue;
            }

            if (stem.Length == 0)
            {
                stem = got;
                firstSub = Sub(visible);
                first = visible;
                continue;
            }

            if (!string.Equals(got, stem, StringComparison.Ordinal))
            {
                return Resolved.Script;
            }

            subDiffers |= !string.Equals(Sub(visible), firstSub, StringComparison.Ordinal);
        }

        if (first is null)
        {
            return null;
        }

        return subDiffers ? new([stem], false) : new(first, false);
    }

    private static string? EchoCommand(CmdSettings settings, string wrapper, string line)
    {
        var stripped = StripAnsi(line);
        if (stripped.StartsWith(' ') || stripped.StartsWith('\t'))
        {
            return null;
        }

        // A long line that starts with a known stem is tool output, not the echoed command.
        var t = stripped.Trim();
        if (t.Length == 0 || Text.Bytes(t) > 400)
        {
            return null;
        }

        if (Wrappers.TaskDollar(t) is { } task)
        {
            return task;
        }

        if (wrapper is not "just")
        {
            return null;
        }

        var words = Text.Words(t);
        if (words.Length == 0)
        {
            return null;
        }

        var stem = Names.CmdStem(words[0]);
        if (stem is "mise" or "just")
        {
            return t;
        }

        return FilterKind(settings, stem) is not "default" && Wrappers.LooksLikeInvocation(words)
            ? t
            : null;
    }

    private sealed record Resolved(List<string> Argv, bool Multi)
    {
        public static Resolved Script => new(["script"], true);
    }
}
