using System.Text;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary><c>rtok run</c>'s capture: stdout then stderr, bounded, with the exit code.</summary>
public sealed class CaptureTests
{
    private static (byte[] Body, int Exit) Sh(string body, long cap) =>
        Capture
            .RunAsync("sh", body, "t…", cap, TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();

    // Bytes past the cap are left out with a notice; the exit code survives the dropped tail.
    [Fact]
    public void A_capped_body_ends_with_the_notice()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "sh");
        var (body, exit) = Sh("head -c 5000 /dev/zero | tr '\\0' x; exit 2", 1024);
        var notice = "[rtok: output capped at 1024 bytes; 3976 bytes not captured]\n";
        Assert.Equal(2, exit);
        Assert.Equal(1024 + 1 + notice.Length, body.Length);
        Assert.Equal(notice, Encoding.UTF8.GetString(body.AsSpan(1025)));
        Assert.All(body.AsSpan(0, 1024).ToArray(), b => Assert.Equal((byte)'x', b));
    }

    [Fact]
    public void A_body_within_the_cap_has_no_notice()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "sh");
        var (body, exit) = Sh("printf hi", 1024);
        Assert.Equal("hi"u8.ToArray(), body);
        Assert.Equal(0, exit);
    }

    // A 40 MiB writer is kept up to the 32 MiB cap and drained, so it never blocks on the pipe.
    [Fact]
    public void A_forty_mib_writer_is_capped_at_thirty_two_and_drained()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "sh");
        var (body, exit) = Sh("head -c 41943040 /dev/zero; echo done >&2", Capture.Cap);
        var notice = "[rtok: output capped at 32 MiB; 8388613 bytes not captured]\n";
        Assert.Equal(0, exit);
        Assert.Equal(Capture.Cap + 1 + notice.Length, body.Length);
        Assert.Equal(notice, Encoding.UTF8.GetString(body.AsSpan((int)Capture.Cap + 1)));
    }

    [Theory]
    [InlineData(new[] { "cargo test" }, "running cargo test…")]
    [InlineData(new[] { "git", "status" }, "running git status…")]
    [InlineData(new[] { "make\nmake install" }, "running make…")]
    public void The_label_names_the_first_line(string[] command, string want) =>
        Assert.Equal(want, Capture.Label(command));

    [Fact]
    public void The_label_keeps_sixty_characters() =>
        Assert.Equal($"running {new string('é', 60)}…", Capture.Label([new string('é', 70)]));
}
