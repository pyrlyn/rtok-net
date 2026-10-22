using System.Globalization;
using System.Text;
using CsCheck;
using Rtok.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>
/// rtok's formatter acceptance rules over every arm: never longer than the raw text, every
/// commit hash, path and WARN/ERROR line kept, a failed run's stderr verdict kept, and a
/// declining formatter handing the text to the rule.
/// </summary>
public sealed class AcceptanceTests
{
    private static string Dir => RepoPaths.Fixture("cmd_golden");

    private static readonly string[] FailVerdicts = ["error: boom", "fatal: bad revision 'x'"];

    private static readonly string[] RuleKinds = ["rule", "unmatched"];

    private static readonly Gen<string> Word = Gen.Char["abcxyz019_-"]
        .Array[1, 8]
        .Select(c => new string(c));

    private static readonly Gen<string> FilePath = Gen.Select(Word.Array[0, 3], Word)
        .Select(t => string.Join('/', t.Item1.Append(t.Item2 + ".rs")));

    private static readonly Gen<string> Hash = Gen.Char["0123456789abcdef"]
        .Array[40]
        .Select(c => new string(c));

    // An argv that reaches each arm; `*` arms get a plain operand.
    private static string[] ArmArgv((string Bin, string Sub) arm) =>
        arm switch
        {
            ("ls", _) => ["ls", "-l"],
            ("grep", _) => ["grep", "-rn", "x", "."],
            ("rg", _) => ["rg", "-n", "x"],
            (var bin, "*") => [bin, "x"],
            var (bin, sub) => [bin, sub],
        };

    // Each arm's golden outputs: the formatter's real input shapes.
    private static readonly Dictionary<(string, string), List<string>> Goldens = LoadGoldens();

    private static Dictionary<(string, string), List<string>> LoadGoldens()
    {
        var map = new Dictionary<(string, string), List<string>>();
        foreach (var file in Directory.EnumerateFiles(Dir, "*.in").Order(StringComparer.Ordinal))
        {
            var (argv, _, _, output) = GoldenTests.ParseIn(File.ReadAllText(file));
            var family = Families.FamilyArgv(argv);
            var (bin, sub) = (Families.Bin(family), Families.Sub(family));
            var arm = Array.Find(
                FamilyFormatters.Arms,
                a =>
                    string.Equals(a.Bin, bin, StringComparison.Ordinal)
                    && (a.Sub is "*" || string.Equals(a.Sub, sub, StringComparison.Ordinal))
            );
            if (arm != default)
            {
                if (!map.TryGetValue(arm, out var list))
                {
                    map[arm] = list = [];
                }

                list.Add(output);
            }
        }

        return map;
    }

    // A golden output with some lines dropped, repeated or replaced by noise.
    private static Gen<(string[] Argv, string Output)> Mutated =>
        Gen.Select(
                Gen.Int[0, FamilyFormatters.Arms.Length - 1],
                Gen.Int[0, 1000],
                Gen.Select(Gen.Int[0, 2], Gen.Int[0, 1000], Gen.String).Array[0, 12]
            )
            .Select(t =>
            {
                var arm = FamilyFormatters.Arms[t.Item1];
                var samples = Goldens.GetValueOrDefault(arm) ?? [""];
                var lines = Text.Lines(samples[t.Item2 % samples.Count]);
                foreach (var (op, pick, noise) in t.Item3)
                {
                    var at = lines.Count == 0 ? 0 : pick % lines.Count;
                    switch (op)
                    {
                        case 0 when lines.Count > 0:
                            lines.RemoveAt(at);
                            break;
                        case 1 when lines.Count > 0:
                            lines.Insert(at, lines[at]);
                            break;
                        default:
                            lines.Insert(at, noise);
                            break;
                    }
                }

                return (ArmArgv(arm), string.Join('\n', lines));
            });

    private static readonly Gen<(string[] Argv, string Output)> AnyArm = Gen.Select(
            Gen.Int[0, FamilyFormatters.Arms.Length - 1],
            Gen.String
        )
        .Select(t => (ArmArgv(FamilyFormatters.Arms[t.Item1]), t.Item2));

    [Fact]
    public void Every_arm_has_golden_inputs_for_the_properties() =>
        Assert.All(FamilyFormatters.Arms, arm => Assert.True(Goldens.ContainsKey(arm), $"{arm}"));

    // For any input each formatter's output is never longer than the raw text.
    [Fact]
    public void No_formatter_is_ever_longer_than_its_input() =>
        Gen.OneOf(AnyArm, Mutated)
            .Sample(t =>
            {
                if (FamilyFormatters.Format(t.Argv, t.Output, 0) is { } got)
                {
                    Assert.True(Text.Bytes(got) <= Text.Bytes(t.Output), got);
                }
            });

    // A WARN or ERROR line planted anywhere in any arm's input is in the formatter's output.
    [Fact]
    public void Every_warn_and_error_line_is_kept() =>
        Gen.Select(
                Mutated,
                Gen.Int[0, 1000],
                Gen.OneOf(Gen.Const("ERROR"), Gen.Const("WARN")),
                Word
            )
            .Sample(t =>
            {
                var ((argv, output), at, level, what) = t;
                var lines = Text.Lines(output);
                var planted = $"{what} {level} {what}";
                lines.Insert(at % (lines.Count + 1), planted);
                if (FamilyFormatters.Format(argv, string.Join('\n', lines), 0) is { } got)
                {
                    Assert.Contains(planted, Text.Lines(got), StringComparer.Ordinal);
                }
            });

    // Every commit's short hash is listed (within the 100-commit cap; past it the rest is
    // counted and `expand` has the original).
    [Fact]
    public void Git_log_keeps_every_commit_hash() =>
        Gen.Select(Hash, Gen.Bool, Word, FilePath.Array[0, 3])
            .Array[1, 40]
            .Sample(commits =>
            {
                var raw = new StringBuilder();
                foreach (var (hash, bob, subject, paths) in commits)
                {
                    raw.Append(
                        CultureInfo.InvariantCulture,
                        $"commit {hash}\nAuthor: {(bob ? "Bob" : "Ann")} <a@x>\nDate:   Wed Oct 14 10:00:00 2026 +0300\n\n    {subject}\n\n"
                    );
                    foreach (var p in paths)
                    {
                        raw.Append(CultureInfo.InvariantCulture, $" {p} | 2 +-\n");
                    }

                    if (paths.Length > 0)
                    {
                        raw.Append(
                            CultureInfo.InvariantCulture,
                            $" {paths.Length} files changed, 1 insertion(+)\n\n"
                        );
                    }
                }

                var got = FamilyFormatters.Format(["git", "log"], raw.ToString(), 0);
                Assert.NotNull(got);
                foreach (var (hash, _, _, paths) in commits)
                {
                    Assert.Contains(hash[..7], got, StringComparison.Ordinal);
                    Assert.All(paths, p => Assert.Contains(p, got, StringComparison.Ordinal));
                }
            });

    // Every changed path is named: in full, or as `dir/{a,b}` with its siblings.
    [Fact]
    public void Git_status_keeps_every_path() =>
        Gen.Select(Gen.OneOfConst(" M", "M ", "MM", "A ", "D ", " D", "??"), FilePath)
            .Array[1, 30]
            .Sample(entries =>
            {
                var raw = new StringBuilder("## main...origin/main [ahead 2]\n");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (xy, p) in entries)
                {
                    if (seen.Add(p))
                    {
                        raw.Append(CultureInfo.InvariantCulture, $"{xy} {p}\n");
                    }
                }

                var got = FamilyFormatters.Format(["git", "status"], raw.ToString(), 0);
                if (got is null)
                {
                    return;
                }

                Assert.All(seen, p => Assert.True(Names(got, p), $"{p} lost:\n{got}"));
            });

    // grep / rg / git grep hit lists name every file, within the budget or as anchors.
    [Fact]
    public void Grep_keeps_every_path() =>
        Gen.Select(FilePath, Gen.Int[1, 400].Array[1, 20])
            .Array[1, 40]
            .Sample(files =>
            {
                var raw = new StringBuilder();
                var paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (p, hits) in files)
                {
                    if (!paths.Add(p))
                    {
                        continue;
                    }

                    foreach (var n in hits)
                    {
                        raw.Append(
                            CultureInfo.InvariantCulture,
                            $"{p}:{n}:    let v = hit({n});\n"
                        );
                    }
                }

                var got = FamilyFormatters.Format(["grep", "-rn", "hit", "."], raw.ToString(), 0);
                if (got is null)
                {
                    return;
                }

                Assert.All(paths, p => Assert.Contains(p, got, StringComparison.Ordinal));
            });

    // Both diff tiers name every file.
    [Fact]
    public void Git_diff_keeps_every_path() =>
        Gen.Select(FilePath, Gen.Int[1, 30])
            .Array[1, 12]
            .Sample(files =>
            {
                var raw = new StringBuilder();
                var paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (p, changes) in files)
                {
                    if (!paths.Add(p))
                    {
                        continue;
                    }

                    raw.Append(
                        CultureInfo.InvariantCulture,
                        $"diff --git a/{p} b/{p}\nindex 1111111..2222222 100644\n--- a/{p}\n+++ b/{p}\n"
                    );
                    raw.Append(
                        CultureInfo.InvariantCulture,
                        $"@@ -1,{changes + 2} +1,{changes + 2} @@\n a\n"
                    );
                    for (var i = 0; i < changes; i++)
                    {
                        raw.Append(CultureInfo.InvariantCulture, $"-old{i}\n+new{i}\n");
                    }

                    raw.Append(" z\n");
                }

                var got = FamilyFormatters.Format(["git", "diff"], raw.ToString(), 0);
                Assert.NotNull(got);
                Assert.All(paths, p => Assert.Contains(p, got, StringComparison.Ordinal));
            });

    // A path printed whole, or under its directory as `dir/{…name…}`.
    private static bool Names(string got, string path)
    {
        if (got.Contains(path, StringComparison.Ordinal))
        {
            return true;
        }

        var cut = path.LastIndexOf('/');
        return cut > 0
            && Text.Lines(got)
                .Exists(l =>
                    l.Contains(path[..cut] + "/{", StringComparison.Ordinal)
                    && l.Contains(path[(cut + 1)..], StringComparison.Ordinal)
                );
    }

    // On a non-zero exit the tool's stderr verdict survives, from the formatter or the rule.
    [Fact]
    public void A_failed_run_keeps_its_stderr_verdict() =>
        Assert.All(
            FamilyFormatters.Arms,
            arm =>
            {
                foreach (var output in Goldens[arm])
                {
                    foreach (var verdict in FailVerdicts)
                    {
                        var raw = output.TrimEnd() + "\n" + verdict + "\n";
                        var (got, _) = Families.Compress(
                            CmdSettings.Builtin(),
                            ArmArgv(arm),
                            raw,
                            1,
                            "id"
                        );
                        Assert.Contains(verdict, Text.Lines(got), StringComparer.Ordinal);
                        if (FamilyFormatters.Format(ArmArgv(arm), raw, 1) is { } formatted)
                        {
                            Assert.Contains(verdict, Text.Lines(formatted), StringComparer.Ordinal);
                        }
                    }
                }
            }
        );

    // A verdict in the middle of a failed run's output either stays in the formatter's text or
    // the formatter declines.
    [Fact]
    public void A_formatter_that_would_drop_a_verdict_declines() =>
        Assert.Null(
            FamilyFormatters.Format(
                ["git", "log"],
                "commit 1111111111111111111111111111111111111111\nAuthor: Ann <a@x>\nDate:   Wed Oct 14 10:00:00 2026 +0300\n\n    first\n\nfatal: bad object HEAD~9\n",
                128
            )
        );

    // A formatter's text is measured as `formatter`; text an arm declines goes to the rule
    // (`rule`, or `unmatched` when the rule shrank nothing). The pre-pass arms (`for`, `mise`)
    // and JSON bodies route before the formatter and are covered by their own tests.
    [Fact]
    public void A_declining_formatter_falls_back_to_the_rule()
    {
        const string Prose =
            "this text is not what the arm parses\nso the arm says no\nand the rule runs\n";
        var declined = 0;
        foreach (var arm in FamilyFormatters.Arms.Where(a => a.Bin is not ("for" or "mise")))
        {
            foreach (var output in Goldens[arm].Append(Prose))
            {
                if (JsonView.Parse(output) is not null)
                {
                    continue;
                }

                var argv = ArmArgv(arm);
                var formatted = FamilyFormatters.Format(argv, output, 0);
                var (_, kind) = Families.Compress(CmdSettings.Builtin(), argv, output, 0, "id");
                if (formatted is null)
                {
                    declined++;
                    Assert.Contains(kind, RuleKinds, StringComparer.Ordinal);
                }
                else
                {
                    Assert.Equal("formatter", kind);
                }
            }
        }

        Assert.True(declined >= 10, $"only {declined} arms declined prose");
    }

    // gh --json, aws and kubectl -o json lose every null, and each line of the compact view
    // still parses: rtok's relaxed JSON (bare keys, `… +N more` and `(N)` size notes) with
    // every string a valid JSON literal.
    [Theory]
    [InlineData("gh_json.in")]
    [InlineData("aws_json.in")]
    [InlineData("kubectl_json.in")]
    public void Json_compaction_strips_nulls_and_still_parses(string file)
    {
        var (argv, exit, _, output) = GoldenTests.ParseIn(
            File.ReadAllText(System.IO.Path.Combine(Dir, file))
        );
        Assert.Contains(": null", output, StringComparison.Ordinal);
        var (got, _) = Families.Compress(CmdSettings.Builtin(), argv, output, exit, "id");
        foreach (var line in Text.Lines(got).Where(l => !l.StartsWith('…')))
        {
            var at = 0;
            if (Relaxed.Key(line, ref at) && line[at..].StartsWith(": ", StringComparison.Ordinal))
            {
                at += 2;
            }
            else
            {
                at = 0;
            }

            Assert.True(Relaxed.Value(line, ref at) && at == line.Length, line);
        }
    }

    // A reader for the compact view's grammar: value = string [" (N)"] | number | true |
    // false | "[" values "]" [" … +N more"] | "{" key ": " value, … "}". No `null`.
    private static class Relaxed
    {
        public static bool Value(string s, ref int at)
        {
            if (at >= s.Length)
            {
                return false;
            }

            switch (s[at])
            {
                case '"':
                    return Str(s, ref at) && Note(s, ref at, " (", ")");
                case '[':
                    return List(s, ref at, ']', Value) && Note(s, ref at, " … +", " more");
                case '{':
                    return List(s, ref at, '}', Member);
                default:
                    return Lit(s, ref at, "true") || Lit(s, ref at, "false") || Number(s, ref at);
            }
        }

        public static bool Key(string s, ref int at)
        {
            var start = at;
            while (
                at < s.Length
                && (
                    char.IsAsciiLetterOrDigit(s[at])
                    || s[at] is '_' or '-' or '.' or '$' or '@' or '/'
                )
            )
            {
                at++;
            }

            if (at == start && at < s.Length && s[at] == '"')
            {
                return Str(s, ref at);
            }

            return at > start;
        }

        private static bool Member(string s, ref int at) =>
            Key(s, ref at) && Lit(s, ref at, ": ") && Value(s, ref at);

        private delegate bool Item(string s, ref int at);

        private static bool List(string s, ref int at, char close, Item item)
        {
            at++;
            if (at < s.Length && s[at] == close)
            {
                at++;
                return true;
            }

            while (item(s, ref at))
            {
                if (at < s.Length && s[at] == close)
                {
                    at++;
                    return true;
                }

                if (!Lit(s, ref at, ", "))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool Str(string s, ref int at)
        {
            var end = at + 1;
            while (end < s.Length && s[end] != '"')
            {
                end += s[end] == '\\' ? 2 : 1;
            }

            if (end >= s.Length)
            {
                return false;
            }

            try
            {
                _ = System.Text.Json.JsonDocument.Parse(s[at..(end + 1)]);
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }

            at = end + 1;
            return true;
        }

        private static bool Number(string s, ref int at)
        {
            var end = at;
            while (
                end < s.Length
                && (char.IsAsciiDigit(s[end]) || s[end] is '-' or '+' or '.' or 'e' or 'E')
            )
            {
                end++;
            }

            if (
                end == at
                || !double.TryParse(
                    s[at..end],
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _
                )
            )
            {
                return false;
            }

            at = end;
            return true;
        }

        // An optional size note after a value; always true, `at` moves only past a whole note.
        private static bool Note(string s, ref int at, string open, string close)
        {
            var start = at;
            if (!Lit(s, ref at, open))
            {
                return true;
            }

            var digits = at;
            while (at < s.Length && char.IsAsciiDigit(s[at]))
            {
                at++;
            }

            if (at > digits && Lit(s, ref at, close))
            {
                return true;
            }

            at = start;
            return true;
        }

        private static bool Lit(string s, ref int at, string lit)
        {
            if (!s.AsSpan(at).StartsWith(lit, StringComparison.Ordinal))
            {
                return false;
            }

            at += lit.Length;
            return true;
        }
    }

    [Fact]
    public Task Git_log_header() =>
        Verify(
            FamilyFormatters.GitLog(
                "commit aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\nAuthor: Ann <a@x>\nDate:   Wed Oct 14 10:00:00 2026 +0300\n\n    Add the parser\n\n src/parse.rs | 40 ++++++++++++++++++++++++++++++++++------\n 1 file changed, 34 insertions(+), 6 deletions(-)\n\n"
                    + "commit bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\nAuthor: Bob <b@x>\nDate:   Tue Oct 13 09:00:00 2026 +0300\n\n    Fix the lexer\n\n src/lex.rs  | 3 ++-\n src/main.rs | 2 +-\n 2 files changed, 3 insertions(+), 2 deletions(-)\n\n"
                    + "commit cccccccccccccccccccccccccccccccccccccccc\nAuthor: Ann <a@x>\nDate:   Mon Oct 12 08:00:00 2026 +0300\n\n    Start\n\n README.md | 1 +\n 1 file changed, 1 insertion(+)\n"
            )
        );

    [Fact]
    public Task Git_status_codes_and_ahead_behind() =>
        Verify(
            FamilyFormatters.Format(
                ["git", "status"],
                "On branch main\nYour branch and 'origin/main' have diverged,\nand have 2 and 3 different commits each, respectively.\n  (use \"git pull\" to merge the remote branch into yours)\n\n"
                    + "Changes to be committed:\n  (use \"git restore --staged <file>...\" to unstage)\n\tnew file:   src/new.rs\n\tmodified:   src/lib.rs\n\n"
                    + "Changes not staged for commit:\n  (use \"git add <file>...\" to update what will be committed)\n\tmodified:   src/main.rs\n\tdeleted:    docs/old.md\n\n"
                    + "Untracked files:\n  (use \"git add <file>...\" to include in what will be committed)\n\tnotes.txt\n",
                0
            )
        );
}
