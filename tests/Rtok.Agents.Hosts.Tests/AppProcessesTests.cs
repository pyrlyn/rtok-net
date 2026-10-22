using Rtok.Agents.Hosts.Restart;
using Rtok.Process;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>The real process table's pieces that run nothing of the user's.</summary>
public sealed class AppProcessesTests
{
    [Theory]
    [InlineData("Cursor", "Cursor.exe")]
    [InlineData("Code.exe", "Code.exe")]
    [InlineData("CODE.EXE", "CODE.EXE")]
    public void A_windows_image_name_gets_exe_once(string name, string image) =>
        Assert.Equal(image, AppProcesses.ImageName(name));

    [Fact]
    public void A_non_zero_exit_fails_with_its_stderr()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "sh");
        var e = Assert.Throws<AgentsException>(() =>
            AppProcesses.Check(ProgressRunner.Quiet("sh", ["-c", "echo nope >&2; exit 3"]), "probe")
        );
        Assert.Equal("probe: exit status: 3 nope", e.Message);
        AppProcesses.Check(ProgressRunner.Quiet("true", []), "probe");
    }

    [Fact]
    public void A_program_that_does_not_start_fails_named()
    {
        var e = Assert.Throws<AgentsException>(() =>
            AppProcesses.Check(ProgressRunner.Quiet("rtok-no-such-program", []), "open -a X")
        );
        Assert.StartsWith("open -a X: ", e.Message, StringComparison.Ordinal);
    }
}
