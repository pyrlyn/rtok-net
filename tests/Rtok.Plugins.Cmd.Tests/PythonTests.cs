using System.Text;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>python.rs</c> tests: pytest sessions, unittest and tracebacks.</summary>
public sealed class PythonTests
{
    private const string Failing = """
        ============================= test session starts ==============================
        platform linux -- Python 3.12.1, pytest-8.1.1
        rootdir: /w
        collected 5 items

        tests/test_a.py ..F                                                      [ 60%]
        tests/test_b.py ..                                                       [100%]

        =================================== FAILURES ===================================
        ________________________________ test_boom _____________________________________

            def test_boom():
        >       assert 1 == 2
        E       assert 1 == 2

        tests/test_a.py:7: AssertionError
        =========================== short test summary info ============================
        FAILED tests/test_a.py::test_boom - assert 1 == 2
        ========================= 1 failed, 4 passed in 0.10s ==========================
        """;

    [Fact]
    public void Failing_session_keeps_failures_and_drops_header_and_passing_progress()
    {
        var got = Python.Pytest(Failing);
        Assert.NotNull(got);
        Assert.Contains("assert 1 == 2", got, StringComparison.Ordinal);
        Assert.Contains("tests/test_a.py:7: AssertionError", got, StringComparison.Ordinal);
        Assert.Contains("FAILED tests/test_a.py::test_boom", got, StringComparison.Ordinal);
        // Named by the FAILED line.
        Assert.DoesNotContain("test_a.py ..F", got, StringComparison.Ordinal);
        Assert.DoesNotContain("platform linux", got, StringComparison.Ordinal);
        Assert.DoesNotContain("test_b.py ..", got, StringComparison.Ordinal);
        Assert.Contains("1 failed, 4 passed in 0.10s", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Passing_session_is_its_totals_line() =>
        Assert.Equal(
            "============================== 3 passed in 0.02s ===============================",
            Python.Pytest(
                """
                ============================= test session starts ==============================
                collected 3 items

                test_a.py ...                                                            [100%]

                ============================== 3 passed in 0.02s ===============================
                """
            )
        );

    [Fact]
    public void Quiet_mode_without_banners_still_parses()
    {
        var got = Python.Pytest(
            "..F.. [100%]\n=================================== FAILURES ===================================\nboom\n1 failed, 4 passed in 0.10s"
        );
        Assert.NotNull(got);
        Assert.Contains("..F..", got, StringComparison.Ordinal);
        Assert.EndsWith("1 failed, 4 passed in 0.10s", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_pytest_text_is_left_alone()
    {
        Assert.Null(Python.Pytest("hello\nworld\n"));
        Assert.Null(Python.Unittest("hello\n"));
    }

    [Fact]
    public void Unittest_drops_a_clean_dots_line_only()
    {
        var ok = Python.Unittest(
            "....\n----------------------------------------------------------------------\nRan 4 tests in 0.001s\n\nOK"
        );
        Assert.NotNull(ok);
        Assert.False(ok.StartsWith("....", StringComparison.Ordinal), ok);
        var bad = Python.Unittest(
            ".F..\n======\nFAIL: test_x\nRan 4 tests in 0.001s\n\nFAILED (failures=1)"
        );
        Assert.NotNull(bad);
        Assert.Contains(".F..", bad, StringComparison.Ordinal);
    }

    [Fact]
    public void Recursion_frames_fold_and_every_other_frame_stays()
    {
        var raw = new StringBuilder(
            "start\nTraceback (most recent call last):\n  File \"m.py\", line 9, in <module>\n    f(3)\n"
        );
        for (var i = 0; i < 40; i++)
        {
            raw.Append("  File \"m.py\", line 5, in f\n    return f(n - 1)\n");
        }

        raw.Append("RecursionError: maximum recursion depth exceeded\nend\n");
        var got = Python.CollapseFrames(raw.ToString());
        Assert.Contains("[previous frame repeated 39 more times]", got, StringComparison.Ordinal);
        Assert.Contains("File \"m.py\", line 9, in <module>", got, StringComparison.Ordinal);
        Assert.Contains("f(3)", got, StringComparison.Ordinal);
        Assert.Contains(
            "RecursionError: maximum recursion depth exceeded",
            got,
            StringComparison.Ordinal
        );
        Assert.StartsWith("start\n", got, StringComparison.Ordinal);
        Assert.EndsWith("end\n", got, StringComparison.Ordinal);
        Assert.True(Text.Lines(got).Count < 12, got);
    }

    [Fact]
    public void Distinct_frames_are_not_folded()
    {
        const string raw =
            "Traceback (most recent call last):\n  File \"a.py\", line 1, in a\n    b()\n  File \"b.py\", line 2, in b\n    c()\nValueError: x\n";
        Assert.Equal(raw, Python.CollapseFrames(raw));
    }
}
