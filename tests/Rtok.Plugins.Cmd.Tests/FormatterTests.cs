using Rtok.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>formatters.rs</c> tests: git log, the table formatters and their stand-downs.</summary>
public sealed class FormatterTests
{
    private static string Dir => RepoPaths.Fixture("cmd_golden");

    private static (string Text, string Kind) Compress(string[] argv, string output, int exit) =>
        Families.Compress(CmdSettings.Builtin(), argv, output, exit, "deadbeef");

    private static string DefaultRule(string output, int exit) =>
        RuleEngine.Apply(CmdSettings.Builtin(), output, exit, new Rule(), "deadbeef");

    // Only `<number> <unit>` shortens; docker's prose forms stay readable words.
    [Theory]
    [InlineData("Up 2 hours", "Up 2h")]
    [InlineData("Exited (0) 3 days ago", "Exited (0) 3d ago")]
    [InlineData("Up 1 week (healthy)", "Up 1w (healthy)")]
    [InlineData("Up About an hour", "Up About an hour")]
    [InlineData("Up About a minute", "Up About a minute")]
    [InlineData("Up Less than a second", "Up Less than a second")]
    public void Shorten_ago_leaves_docker_prose_durations_whole(string raw, string want) =>
        Assert.Equal(want, Tables.ShortenAgo(raw));

    // kubectl ≥1.22 prints RESTARTS as `1 (3m ago)`: three tokens in one column, so later
    // columns are found by the header's offsets, not by token index.
    [Fact]
    public void Kubectl_get_wide_keeps_ip_after_a_restart_with_age() =>
        Assert.Equal(
            "NAME READY STATUS IP\np 1/1 Running 10.0.0.5\nq 0/1 Pending <none>",
            Tables.KubectlGet(
                "NAME    READY   STATUS    RESTARTS     AGE   IP         NODE\n"
                    + "p       1/1     Running   1 (3m ago)   10d   10.0.0.5   n1\n"
                    + "q       0/1     Pending   0            1m    <none>     <none>\n"
            )
        );

    [Theory]
    [InlineData("docker_ps.in", "docker ps", "web-")]
    [InlineData("podman_ps.in", "podman ps", "web-")]
    [InlineData("kubectl_get.in", "kubectl get", "web-deploy-")]
    [InlineData("ps_aux.in", "ps aux", "worker-")]
    public void Table_formatter_beats_default_rule_and_keeps_every_object(
        string file,
        string argv,
        string prefix
    )
    {
        var (_, exit, _, output) = GoldenTests.ParseIn(File.ReadAllText(Path.Combine(Dir, file)));
        var (got, kind) = Compress(Text.Words(argv), output, exit);
        Assert.Equal("formatter", kind);
        Assert.True(Text.Bytes(got) < Text.Bytes(DefaultRule(output, exit)));
        for (var i = 0; i < 40; i++)
        {
            var name = prefix switch
            {
                "web-" => $"web-{i:D2}",
                "web-deploy-" => $"web-deploy-{i:D2}-abcd",
                _ => $"worker-{i}",
            };
            Assert.Contains(name, got, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("docker ps", "Cannot connect to the Docker daemon\n", 0, "Cannot connect")]
    [InlineData("podman ps", "Cannot connect to the Podman daemon\n", 0, "Cannot connect")]
    [InlineData(
        "kubectl get",
        "error: the server doesn't have a resource type \"pods\"\n",
        1,
        "doesn't have a resource type"
    )]
    [InlineData("ps aux", "ps: invalid option -- z\n", 1, "invalid option")]
    [InlineData("df -h", "df: /nope: No such file or directory\n", 1, "No such file")]
    [InlineData("sqlite3 db", "1|alpha\n2|beta\n", 0, "1|alpha")]
    public void Table_formatter_stands_down_on_unrecognized(
        string argv,
        string output,
        int exit,
        string kept
    )
    {
        var (got, kind) = Compress(Text.Words(argv), output, exit);
        Assert.NotEqual("formatter", kind, StringComparer.Ordinal);
        Assert.Contains(kept, got, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("df.in")]
    [InlineData("sqlite3.in")]
    [InlineData("awk.in")]
    public void Df_sqlite_and_awk_beat_the_default_rule(string file)
    {
        var (argv, exit, _, output) = GoldenTests.ParseIn(
            File.ReadAllText(Path.Combine(Dir, file))
        );
        var (got, _) = Compress(argv, output, exit);
        Assert.True(Text.Bytes(got) < Text.Bytes(DefaultRule(output, exit)), got);
    }

    [Fact]
    public void Ps_aux_skips_a_bad_line_instead_of_dropping_every_row()
    {
        var got = Tables.PsAux(
            "USER PID %CPU COMMAND\nroot 1 0.0 /sbin/init\nUSER PID %CPU COMMAND\nroot 42 0.1 /usr/bin/worker-7\n"
        );
        Assert.NotNull(got);
        Assert.Contains("1 init", got, StringComparison.Ordinal);
        Assert.Contains("42 worker-7", got, StringComparison.Ordinal);
        Assert.DoesNotContain("PID", got, StringComparison.Ordinal);
    }

    private static string Commit(char sha, string subject, string extra = "") =>
        $"commit {new string(sha, 40)}\nAuthor: Ann <a@x>\nDate:   Wed Oct 14 10:00:00 2026 +0300\n\n    {subject}\n\n{extra}";

    [Fact]
    public void Git_log_lists_every_commit_with_authors_when_mixed() =>
        Assert.Equal(
            "[2 commits] Ann unless named\n1111111 Oct 14 2026 first\n2222222 Oct 13 2025 second — Bob",
            FamilyFormatters.GitLog(
                "commit 1111111111111111111111111111111111111111\nAuthor: Ann <a@x>\nDate:   Wed Oct 14 10:00:00 2026 +0300\n\n    first\n\n    body line\n\ncommit 2222222222222222222222222222222222222222\nAuthor: Bob <b@x>\nDate:   Tue Oct 13 10:00:00 2025 +0300\n\n    second\n"
            )
        );

    [Fact]
    public void Git_log_keeps_stat_lines_under_their_commit()
    {
        var got = FamilyFormatters.GitLog(
            Commit(
                '1',
                "first",
                " src/a.rs | 2 +-\n 1 file changed, 1 insertion(+), 1 deletion(-)\n"
            )
        );
        Assert.NotNull(got);
        Assert.Contains("1111111 Oct 14 first", got, StringComparison.Ordinal);
        Assert.Contains("  src/a.rs | 2 +-", got, StringComparison.Ordinal);
        Assert.Contains("  1 file changed", got, StringComparison.Ordinal);
    }

    // The first line counts the commits and, when `--stat` printed totals, sums them.
    [Fact]
    public void Git_log_header_counts_commits_and_stat_totals()
    {
        var raw =
            Commit(
                '1',
                "s1",
                " src/a.rs | 3 ++-\n 1 file changed, 2 insertions(+), 1 deletion(-)\n\n"
            ) + Commit('2', "s2", " src/a.rs | 3 ++-\n 1 file changed, 3 insertions(+)\n\n");
        Assert.Equal("[2 commits, +5/-1] Ann · 2026", Text.Lines(FamilyFormatters.GitLog(raw)!)[0]);
        // Without stat totals there is nothing to sum, so only the count is claimed.
        Assert.Equal(
            "[1 commit] Ann · 2026",
            Text.Lines(FamilyFormatters.GitLog(Commit('1', "one"))!)[0]
        );
    }

    // One author is named once in the header, never on a commit line.
    [Fact]
    public void Git_log_drops_the_author_when_all_are_equal()
    {
        var got = FamilyFormatters.GitLog(Commit('1', "s1") + Commit('2', "s2"));
        Assert.NotNull(got);
        Assert.Single(got.Split("Ann")[1..]);
        Assert.DoesNotContain(" — ", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Git_log_other_shapes_keep_the_head_cut()
    {
        Assert.Null(FamilyFormatters.GitLog("1111111 first\n2222222 second\n"));
        Assert.Null(FamilyFormatters.GitLog(Commit('1', "first", "diff --git a/x b/x\n")));
    }

    [Fact]
    public void Git_log_caps_the_listing()
    {
        var raw = string.Concat(
            Enumerable.Repeat(Commit('1', "c"), FamilyFormatters.GitLogMaxCommits + 5)
        );
        var got = FamilyFormatters.GitLog(raw);
        Assert.NotNull(got);
        Assert.EndsWith("… +5 more", got, StringComparison.Ordinal);
        Assert.Equal(FamilyFormatters.GitLogMaxCommits + 2, Text.Lines(got).Count);
    }
}
