namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>loops.rs</c> tests: shell loops whose lines differ only in fields.</summary>
public sealed class LoopsTests
{
    [Fact]
    public void Polling_lines_collapse_with_first_and_last_values() =>
        Assert.Equal(
            "attempt #: status=pending ×20 (vals: 1..20)",
            Loops.Collapse(
                string.Concat(Enumerable.Range(1, 20).Select(i => $"attempt {i}: status=pending\n"))
            )
        );

    [Fact]
    public void A_changed_line_ends_the_run_and_stays_verbatim() =>
        Assert.Equal(
            "attempt #: status=pending ×9 (vals: 1..9)\nattempt 10: status=ready\nattempt 11: status=ready",
            Loops.Collapse(
                string.Concat(Enumerable.Range(1, 9).Select(i => $"attempt {i}: status=pending\n"))
                    + "attempt 10: status=ready\nattempt 11: status=ready\n"
            )
        );

    [Fact]
    public void Error_lines_are_never_collapsed_and_split_the_run() =>
        Assert.Equal(
            "poll # ok ×3 (vals: 1..3)\npoll 4 failed: connection refused\npoll 5 failed: connection refused\npoll # ok ×3 (vals: 6..8)",
            Loops.Collapse(
                "poll 1 ok\npoll 2 ok\npoll 3 ok\npoll 4 failed: connection refused\npoll 5 failed: connection refused\npoll 6 ok\npoll 7 ok\npoll 8 ok\n"
            )
        );

    [Fact]
    public void Identical_lines_and_blank_separators_collapse_to_a_count() =>
        Assert.Equal(
            "{\"status\":\"starting\"} ×3\n\n{\"status\":\"up\"}",
            Loops.Collapse(
                "{\"status\":\"starting\"}\n\n{\"status\":\"starting\"}\n\n{\"status\":\"starting\"}\n\n{\"status\":\"up\"}\n"
            )
        );

    [Fact]
    public void A_number_that_goes_up_and_down_shows_its_smallest_and_largest() =>
        Assert.Equal(
            "took # ms ×4 (vals: 3..9)",
            Loops.Collapse("took 5 ms\ntook 9 ms\ntook 3 ms\ntook 7 ms\n")
        );

    [Fact]
    public void Two_lines_are_left_alone()
    {
        Assert.Null(Loops.Collapse("poll 1\npoll 2\n"));
        Assert.Null(Loops.Collapse(""));
    }

    [Fact]
    public void Ids_and_timestamps_are_fields_too() =>
        Assert.Equal(
            "12:00:# job # ready ×3 (vals: 01..03, 9f8e7d6c5b4a..aabbccddeeff)",
            Loops.Collapse(
                "12:00:01 job 9f8e7d6c5b4a ready\n12:00:02 job 1a2b3c4d5e6f ready\n12:00:03 job aabbccddeeff ready\n"
            )
        );

    // T18 criterion: ERROR / WARN lines inside a run are never folded into it.
    [Fact]
    public void Error_and_warn_lines_inside_a_run_stay_verbatim()
    {
        var got = Loops.Collapse(
            "try 1 ok\ntry 2 ok\ntry 3 ERROR timeout\ntry 4 WARN slow\ntry 5 ok\ntry 6 ok\ntry 7 ok\n"
        );
        Assert.NotNull(got);
        Assert.Contains("try 3 ERROR timeout", got, StringComparison.Ordinal);
        Assert.Contains("try 4 WARN slow", got, StringComparison.Ordinal);
    }
}
