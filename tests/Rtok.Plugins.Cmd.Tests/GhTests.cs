using System.Globalization;
using System.Text;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>gh.rs</c> tests: pr/issue view, pr checks and run logs.</summary>
public sealed class GhTests
{
    private static string[] Argv(string s) => Text.Words(s);

    private const string View =
        "title:\tFix the thing\nstate:\tOPEN\nauthor:\tada\nlabels:\t\nassignees:\t\nreviewers:\t\nprojects:\t\nmilestone:\t\nnumber:\t12\nurl:\thttps://github.com/o/r/pull/12\nadditions:\t10\ndeletions:\t2\n--\nFirst paragraph.   \n\n\n\nSecond paragraph.\n\n\n";

    [Fact]
    public void View_drops_empty_fields_and_blank_runs_but_keeps_the_rest()
    {
        var got = Gh.Format(Argv("gh pr view 12"), View);
        Assert.NotNull(got);
        foreach (
            var kept in new[]
            {
                "title:\tFix the thing",
                "number:\t12",
                "url:\thttps://github.com/o/r/pull/12",
                "additions:\t10",
            }
        )
        {
            Assert.Contains(kept, got, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("labels:", got, StringComparison.Ordinal);
        Assert.DoesNotContain("milestone:", got, StringComparison.Ordinal);
        Assert.EndsWith("First paragraph.\n\nSecond paragraph.", got, StringComparison.Ordinal);
    }

    [Fact]
    public void View_of_json_or_other_shapes_stands_down()
    {
        Assert.Null(Gh.View("{\"title\":\"x\"}"));
        Assert.Null(Gh.View(""));
    }

    private const string Checks =
        "build\tpass\t1m2s\thttps://ci/1\nlint\tpass\t10s\thttps://ci/2\ntest (ubuntu)\tfail\t3m\thttps://ci/3\ntest (mac)\tpending\t0\thttps://ci/4\ndocs\tskipping\t0\thttps://ci/5\ncodeql\tpass\t5m\thttps://ci/6\n";

    [Fact]
    public void Checks_keep_failing_and_pending_rows_with_urls_and_count_the_rest()
    {
        var got = Gh.Format(Argv("gh pr checks"), Checks);
        Assert.NotNull(got);
        Assert.StartsWith(
            "6 checks: 3 pass, 1 fail, 1 pending, 1 skipping",
            got,
            StringComparison.Ordinal
        );
        Assert.Contains("test (ubuntu)\tfail\t3m\thttps://ci/3", got, StringComparison.Ordinal);
        Assert.Contains("test (mac)\tpending", got, StringComparison.Ordinal);
        Assert.DoesNotContain("lint", got, StringComparison.Ordinal);
        Assert.DoesNotContain("codeql", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Checks_with_an_unknown_state_stand_down() =>
        Assert.Null(Gh.Checks("a\tweird\t1s\thttps://x\n"));

    private static string Log()
    {
        var s = new StringBuilder();
        for (var i = 0; i < 30; i++)
        {
            s.Append(
                CultureInfo.InvariantCulture,
                $"build\tSet up job\t2026-10-15T10:00:{i:D2}.1234567Z line {i}\n"
            );
        }

        for (var i = 0; i < 12; i++)
        {
            s.Append(
                CultureInfo.InvariantCulture,
                $"build\tRun tests\t2026-10-15T10:01:{i:D2}.1234567Z compiling crate {i}\n"
            );
        }

        s.Append("build\tRun tests\t2026-10-15T10:02:00.0000000Z test a ... ok\n");
        s.Append(
            "build\tRun tests\t2026-10-15T10:02:01.0000000Z ##[error]test b failed: expected 1\n"
        );
        s.Append("build\tRun tests\t2026-10-15T10:02:02.0000000Z summary line\n");
        for (var i = 0; i < 12; i++)
        {
            s.Append(
                CultureInfo.InvariantCulture,
                $"build\tRun tests\t2026-10-15T10:03:{i:D2}.1234567Z tail noise {i}\n"
            );
        }

        return s.ToString();
    }

    [Fact]
    public void Run_log_keeps_the_error_with_context_and_counts_quiet_steps()
    {
        var raw = Log();
        var got = Gh.Format(Argv("gh run view 7 --log"), raw);
        Assert.NotNull(got);
        Assert.Contains("## build / Run tests", got, StringComparison.Ordinal);
        Assert.Contains("##[error]test b failed: expected 1", got, StringComparison.Ordinal);
        Assert.Contains("test a ... ok", got, StringComparison.Ordinal);
        Assert.Contains("summary line", got, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-15T", got, StringComparison.Ordinal);
        Assert.Contains("1 steps without errors (30 lines)", got, StringComparison.Ordinal);
        Assert.True(Text.Lines(got).Count < Text.Lines(raw).Count / 2, got);
    }

    [Fact]
    public void Warnings_in_a_quiet_looking_step_are_kept_not_counted_away()
    {
        var got = Gh.RunLog(
            "b\tbuild\t2026-10-15T10:00:00.0Z warning: unused import\nb\tbuild\t2026-10-15T10:00:01.0Z ok\n"
        );
        Assert.NotNull(got);
        Assert.Contains("warning: unused import", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_that_is_not_shorter_is_left_to_the_rule()
    {
        Assert.Null(Gh.Format(Argv("gh pr checks"), "a\tfail\t1s\thttps://x\n"));
        Assert.Null(Gh.Format(Argv("gh repo view"), View));
    }
}
