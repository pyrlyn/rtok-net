using Rtok.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>Formatter dispatch: families, wrapper peeling and the <c>[script]</c> rule (rtok's formatters.rs tests).</summary>
public sealed class DispatchTests
{
    private static readonly CmdSettings Settings = CmdSettings.Builtin();

    private const string Failed = "test a::bad ... FAILED\ntest result: FAILED. 1 failed\n";

    private static string[] Words(string s) =>
        s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string Lines(string format, int n) =>
        string.Join(
            '\n',
            Enumerable
                .Range(0, n)
                .Select(i =>
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, format, i)
                )
        );

    [Fact]
    public void Rule_is_picked_by_the_command_not_an_argument()
    {
        Assert.Equal(
            "grep",
            Settings.Pick(Families.Bin(Families.FamilyArgv(["/usr/bin/grep", "-rn", "x"]))).MatchCmd
        );
        Assert.Equal(
            "",
            Settings
                .Pick(Families.Bin(Families.FamilyArgv(["git", "commit", "-m", "fix grep"])))
                .MatchCmd
        );
        Assert.Equal(
            "",
            Settings.Pick(Families.Bin(Families.FamilyArgv(["docker", "run", "node"]))).MatchCmd
        );
    }

    // A single Bash string joining 2+ programs takes `[script]`, not the first program's family.
    [Fact]
    public void Newline_joined_script_uses_the_script_rule_not_the_first_command()
    {
        var script = "git status\ncat file.txt\n" + Lines("line {0} of mixed output", 40);
        var (got, kind) = Families.Compress(Settings, [script], script, 0, "id");
        Assert.Equal("rule", kind);
        Assert.True(got.Length < script.Length, got);
        Assert.Contains("omitted (expand id)", got, StringComparison.Ordinal);
    }

    // The tests below need the family formatter arms; T18 removes the skips.
    internal const string PendingT18 = "family formatter arms: T18";

    [Fact]
    public void One_line_git_status_keeps_its_own_family()
    {
        Assert.Skip(PendingT18);
        var (_, kind) = Families.Compress(Settings, ["git status"], "clean\n", 0, "id");
        Assert.NotEqual("rule", kind, StringComparer.Ordinal);
    }

    // A chain of the same program keeps that program's formatter, not `[script]`.
    [Theory]
    [InlineData("cargo build && cargo test")]
    [InlineData("cd crates/rtok && cargo test")]
    public void Same_program_chain_keeps_its_own_family_not_script(string cmd)
    {
        Assert.Skip(PendingT18);
        var (got, kind) = Families.Compress(
            Settings,
            [cmd],
            Lines("warning: unused variable `x{0}`", 40),
            0,
            "id"
        );
        Assert.NotEqual("rule", kind, StringComparer.Ordinal);
        Assert.NotEmpty(got);
    }

    [Theory]
    [InlineData("mise exec -- cargo test", Failed)]
    [InlineData("mise x node@20 -- cargo test", Failed)]
    [InlineData("mise exec -c 'cargo test'", Failed)]
    [InlineData("just --command cargo test", Failed)]
    [InlineData("just -c cargo test", Failed)]
    [InlineData("cd /tmp && mise exec -- cargo test", Failed)]
    [InlineData("mise run test", "[test] $ cargo test\n" + Failed)]
    [InlineData("just test", "mise exec -- cargo test\n" + Failed)]
    public void Wrappers_use_the_inner_formatter_when_the_command_is_visible(
        string cmd,
        string output
    )
    {
        Assert.Skip(PendingT18);
        var (got, kind) = Families.Compress(Settings, [cmd], output, 0, "id");
        Assert.Equal("formatter", kind);
        Assert.Contains("FAILED", got, StringComparison.Ordinal);
    }

    // Quiet runs print no command line, so the wrapper stem stays.
    [Theory]
    [InlineData("mise run test")]
    [InlineData("just test")]
    public void Quiet_wrappers_keep_the_wrapper_family(string cmd) =>
        Assert.NotEqual(
            "formatter",
            Families.Compress(Settings, [cmd], Failed, 0, "id").Kind,
            StringComparer.Ordinal
        );

    [Fact]
    public void A_picked_rule_that_shrinks_nothing_is_unmatched_not_raw()
    {
        // `cat` has a named rule (`max_lines = 80`); 5 lines never reach the cut.
        const string body = "one\ntwo\nthree\nfour\nfive";
        var (got, kind) = Families.Compress(Settings, ["cat", "f"], body, 0, "id");
        Assert.Equal(body, got);
        Assert.Equal("unmatched", kind);
    }

    [Fact]
    public void Unbounded_multi_file_cat_gets_the_cat_rule()
    {
        var body = Lines("line {0}", 200);
        var (got, kind) = Families.Compress(Settings, ["cat -n a.rs b.rs c.rs"], body, 0, "id");
        Assert.Equal("rule", kind);
        Assert.True(got.Length < body.Length, got);
    }

    [Fact]
    public void Large_recursive_grep_hit_list_gets_the_grep_rule()
    {
        var body = Lines("src/f{0}.rs:{0}:needle found here", 200);
        var (got, kind) = Families.Compress(Settings, ["grep -rn -A3 needle src"], body, 0, "id");
        Assert.Equal("rule", kind);
        Assert.True(got.Length < body.Length, got);
    }

    [Fact]
    public void One_quoted_argv_filters_like_a_split_argv()
    {
        var checkedCount = 0;
        foreach (var path in Directory.EnumerateFiles(RepoPaths.Fixture("cmd_golden"), "*.in"))
        {
            var (argv, exit, _, output) = GoldenTests.ParseIn(File.ReadAllText(path));
            if (argv.Length == 0)
            {
                continue;
            }

            checkedCount++;
            Assert.Equal(
                Families.Compress(Settings, argv, output, exit, "id"),
                Families.Compress(Settings, [string.Join(' ', argv)], output, exit, "id")
            );
        }

        Assert.True(checkedCount >= 10, $"expected the golden families, saw {checkedCount}");
    }

    [Theory]
    [InlineData("git", new[] { "git status | head" })]
    [InlineData("git", new[] { "/usr/bin/git", "status" })]
    [InlineData("other", new string[0])]
    [InlineData("git", new[] { @"C:\tools\git.exe", "status" })]
    [InlineData("cargo", new[] { "cargo.exe", "test" })]
    public void Family_names_the_command(string want, string[] argv) =>
        Assert.Equal(want, Families.Family(argv));

    [Fact]
    public void Two_echoed_programs_use_the_script_rule()
    {
        var body = Lines("line {0} of mixed output", 40);
        var output = $"mise exec -- git status\n{body}\nmise exec -- cargo test\n{body}";
        var (got, kind) = Families.Compress(Settings, ["just check"], output, 0, "id");
        Assert.Equal("script", Families.FamilyOf(Settings, ["just check"], output));
        Assert.Equal("rule", kind);
        Assert.Contains("omitted", got, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cargo", "mise exec -- cargo test", Failed)]
    [InlineData("git", "mise x -C /tmp -- git status", "On branch\n")]
    [InlineData("node", "mise exec node@20 -- node -v", "v20\n")]
    [InlineData("cargo", "just -c cargo test", Failed)]
    [InlineData("mise", "mise exec -c", Failed)]
    [InlineData("cargo", "mise run test", "[test] $ cargo test\n" + Failed)]
    [InlineData("mise", "mise run test", Failed)]
    [InlineData("mise", "mise exec cargo test", Failed)]
    [InlineData("git", "just status", "git status\nOn branch main\n")]
    [InlineData("git", "just status", "\u001b[32mgit status\u001b[0m\nOn branch main\n")]
    [InlineData("just", "just check", "Checking foo v0.1.0\nFinished dev\n")]
    [InlineData("script", "just check", "mise exec -- cargo test\nmise exec -- git status\n")]
    [InlineData("cargo", "mise exec -- just test", "[test] $ cargo test\n" + Failed)]
    [InlineData("cargo", "uv run cargo test", "ok\n")]
    [InlineData("cargo", "FOO=1 cargo test", "ok\n")]
    [InlineData("pytest", ".venv/bin/pytest -q", "ok\n")]
    [InlineData("pytest", "uv run python -m pytest -q", "ok\n")]
    [InlineData("mise", "mise exec --", "ok\n")]
    public void Wrappers_use_the_inner_command_when_it_is_visible(
        string want,
        string cmd,
        string output
    ) => Assert.Equal(want, Families.FamilyOf(Settings, Words(cmd), output));

    [Theory]
    [InlineData("cargo", "cd crates/x && cargo test")]
    [InlineData("cargo", "cd a && cd b && cargo test")]
    [InlineData("cargo", "A=1 B=2 cargo test")]
    [InlineData("git", "git commit -m \"fix grep\"")]
    public void The_family_comes_after_cd_hops_and_env_prefixes(string want, string cmd) =>
        Assert.Equal(want, Families.FamilyOf(Settings, [cmd], "ok\n"));

    [Fact]
    public void A_quoted_pipe_does_not_split_the_family() =>
        Assert.Equal("git", Families.FamilyOf(Settings, ["git commit -m \"a|b\""], "ok\n"));

    [Fact]
    public void Every_builtin_rule_family_has_a_golden()
    {
        var argvs = Directory
            .EnumerateFiles(RepoPaths.Fixture("cmd_golden"), "*.in")
            .Select(p => GoldenTests.ParseIn(File.ReadAllText(p)).Argv)
            .Where(a => a.Length > 0)
            .Select(a => Families.Bin(Families.FamilyArgv(a)))
            .ToHashSet(StringComparer.Ordinal);
        var missing = RuleParser
            .Defaults()
            .Select(r => r.MatchCmd)
            .Where(f => !string.Equals(f, "script", StringComparison.Ordinal) && !argvs.Contains(f))
            .ToList();
        Assert.Empty(missing);
    }
}
