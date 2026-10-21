using Rtok.Cli.Ui;
using Rtok.Process;

namespace Rtok.Cli.Tests;

public sealed class TerminalDisplayTests
{
    [Fact]
    public void A_spinner_draws_its_label_and_erases_itself_when_it_finishes()
    {
        var (display, output) = Display();

        var spinner = display.Spinner("cloning…");
        spinner.Message("x");
        spinner.Finish();

        Assert.Equal("⠋ cloning…\r\e[J⠋ cloning… x\r\e[J", output.ToString());
    }

    [Fact]
    public void A_batch_bar_keeps_the_spinner_of_its_command_under_it()
    {
        var (display, output) = Display();
        var bar = display.Bar("agents", 4, ProgressUnit.Items);
        bar.Report(new ProgressUpdate(1, 4, ProgressUnit.Items, "claude"));
        output.GetStringBuilder().Clear();

        var spinner = display.Spinner("npm…");
        display.Tick();

        Assert.Equal(
            "\r\e[J[=====>              ] agents 1/4 claude\n⠋ npm…"
                + "\r\e[1A\e[J[=====>              ] agents 1/4 claude\n⠙ npm…",
            output.ToString()
        );
        spinner.Finish();
        bar.Finish();
    }

    [Fact]
    public void A_spinner_that_learns_a_total_becomes_a_bar_and_lines_fit_the_width()
    {
        var (display, output) = Display(width: 30);
        var spinner = display.Spinner("downloading");
        output.GetStringBuilder().Clear();

        spinner.Report(
            new ProgressUpdate(3 * 1024 * 1024, 4 * 1024 * 1024, ProgressUnit.Bytes, "a")
        );

        Assert.Equal("\r\e[J[===============>    ] downlo", output.ToString());
        spinner.Finish();
    }

    [Fact]
    public void Writes_happen_with_the_lines_erased_and_the_lines_come_back_after()
    {
        var (display, output) = Display();
        var spinner = display.Spinner("x");

        display.Suspend(() => output.Write("out\n"));

        Assert.Equal("⠋ x\r\e[Jout\n⠋ x", output.ToString());
        spinner.Finish();
    }

    [Fact]
    public void Colour_paints_only_the_spinner_or_the_bar()
    {
        var output = new StringWriter();
        var display = new TerminalDisplay(output, () => 80, () => true, animate: false);

        display.Spinner("x").Finish();

        Assert.StartsWith("\e[36m⠋\e[0m x", output.ToString(), StringComparison.Ordinal);
    }

    private static (TerminalDisplay Display, StringWriter Output) Display(int width = 80)
    {
        var output = new StringWriter();
        return (new TerminalDisplay(output, () => width, () => false, animate: false), output);
    }
}
