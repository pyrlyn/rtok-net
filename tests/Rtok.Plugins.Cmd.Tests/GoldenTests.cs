using Rtok.Core;
using Rtok.Plugin.Sdk;
using Rtok.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>tests/cmd_golden</c>: each <c>.in</c> compresses to its <c>.out</c> and clears its <c>min_saving</c> floor.</summary>
public sealed class GoldenTests
{
    // AWS's documented example access key id, planted in `cat.in`: formatters compress, they
    // do not redact.
    private const string AwsExampleKeyId = "AKIAIOSFODNN7EXAMPLE";

    // Fixed rates, so a config default change cannot shift the floors.
    private static readonly EstimatorRates SavingRates = new(3.5f, 4.2f, 3.0f, 1.0f);

    // Fixtures whose output comes from a family formatter arm or the mise / loop tidy in
    // front of the route (T18). T18 empties this set.
    private static readonly HashSet<string> PendingT18 = new(StringComparer.Ordinal)
    {
        "cargo_build",
        "cargo_build_ok",
        "cargo_clippy",
        "cargo_test",
        "cargo_test_panic",
        "df",
        "docker_ps",
        "for_poll",
        "gh_checks",
        "gh_pr_view",
        "gh_run_log",
        "git_diff",
        "git_diff_big",
        "git_log",
        "git_log_long",
        "git_show",
        "git_status",
        "git_status_many",
        "git_status_staged",
        "go_test",
        "grep_group",
        "jest",
        "just_command",
        "just_git",
        "just_recipe",
        "kubectl_get",
        "ls_la",
        "ls_many",
        "mise_exec",
        "mise_install",
        "mise_run",
        "mise_run_install",
        "mise_x",
        "mise_x_git",
        "podman_ps",
        "ps_aux",
        "py_recursion",
        "pytest",
        "pytest_fail",
        "python3_unittest",
        "python_recursion",
        "sqlite3",
        "tree",
        "until_pending",
        "vitest",
        "wc",
        "while_check",
    };

    private static string Dir => RepoPaths.Fixture("cmd_golden");

    public static TheoryData<string> Fixtures()
    {
        var names = Directory
            .EnumerateFiles(Dir, "*.in")
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Order(StringComparer.Ordinal)
            .ToList();
        return [.. names];
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_compresses_to_its_out_and_clears_its_floor(string name)
    {
        if (PendingT18.Contains(name))
        {
            Assert.Skip("family formatter arm: T18");
        }

        var (argv, exit, minSaving, output) = ParseIn(
            File.ReadAllText(Path.Combine(Dir, name + ".in"))
        );
        var (got, _) = Families.Compress(CmdSettings.Builtin(), argv, output, exit, "deadbeef");
        var want = File.ReadAllText(Path.Combine(Dir, name + ".out"));
        Assert.Equal(want.TrimEnd(), got.TrimEnd());

        Assert.NotNull(minSaving);
        var before = TokenEstimator.Estimate(output, TextClass.Code, SavingRates);
        var after = TokenEstimator.Estimate(got, TextClass.Code, SavingRates);
        Assert.True(after <= before, $"output ({after} tok) is larger than input ({before} tok)");
        var pct = before == 0 ? 0 : (before - after) * 100L / before;
        Assert.True(pct >= minSaving, $"saving {pct}% below floor {minSaving}%");
    }

    [Fact]
    public void Every_fixture_has_an_out_and_there_are_ten_families()
    {
        var ins = Directory.GetFiles(Dir, "*.in");
        Assert.True(ins.Length >= 10);
        Assert.All(ins, p => Assert.True(File.Exists(Path.ChangeExtension(p, ".out")), p));
    }

    [Fact]
    public void Pending_sets_name_real_fixtures() =>
        Assert.All(PendingT18, n => Assert.True(File.Exists(Path.Combine(Dir, n + ".in")), n));

    [Fact]
    public void Cat_keeps_the_aws_key_line()
    {
        var secret = File.ReadAllText(Path.Combine(Dir, "cat.in"));
        Assert.Contains(AwsExampleKeyId, secret, StringComparison.Ordinal);
        var (got, _) = Families.Compress(
            CmdSettings.Builtin(),
            ["cat", "secrets.env"],
            ParseIn(secret).Output,
            0,
            "id"
        );

        // Never echo `got`: it carries the credential-shaped fixture.
        Assert.True(got.Contains(AwsExampleKeyId, StringComparison.Ordinal));
    }

    // `argv: 'a && b'` (single-quoted) is ONE argv element, the shape the hook sends; plain
    // `argv: a b c` splits on whitespace.
    internal static (string[] Argv, int Exit, int? MinSaving, string Output) ParseIn(string s)
    {
        string[] argv = [];
        var exit = 0;
        int? minSaving = null;
        var rest = s;
        foreach (var line in s.Split('\n'))
        {
            if (line.StartsWith("argv: ", StringComparison.Ordinal))
            {
                var a = line[6..];
                argv =
                    a.Length >= 2 && a.StartsWith('\'') && a.EndsWith('\'')
                        ? [a[1..^1]]
                        : a.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (line.StartsWith("exit: ", StringComparison.Ordinal))
            {
                exit = int.TryParse(
                    line[6..],
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var e
                )
                    ? e
                    : 0;
            }
            else if (line.StartsWith("min_saving: ", StringComparison.Ordinal))
            {
                minSaving = int.TryParse(
                    line[12..].Trim(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var m
                )
                    ? m
                    : null;
            }
            else if (string.Equals(line, "---", StringComparison.Ordinal))
            {
                var at = s.IndexOf("---\n", StringComparison.Ordinal);
                rest = at < 0 ? "" : s[(at + 4)..];
                break;
            }
        }

        return (argv, exit, minSaving, rest);
    }
}
