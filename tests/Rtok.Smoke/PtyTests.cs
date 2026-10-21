using System.Text;
using Porta.Pty;
using Rtok.Testing;

namespace Rtok.Smoke;

// Interactive checks run the binary in a pseudoterminal, the way a person's shell does.
public sealed class PtyTests
{
    [Fact]
    public async Task Version_prints_on_a_terminal()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync("rtok", ct);
        using var pty = await PtyProvider.SpawnAsync(
            new PtyOptions
            {
                App = rtok,
                CommandLine = ["--version"],
                Cwd = RepoPaths.Root,
                Cols = 80,
                Rows = 24,
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            ct
        );

        var output = await ReadToExitAsync(pty, ct);

        Assert.True(pty.WaitForExit(10_000));
        Assert.Equal(0, pty.ExitCode);
        // The runtime may first switch the terminal to keypad mode (ESC [?1h ESC =).
        Assert.Matches(@"rtok \d+\.\d+\.\d+ ", output);
    }

    // A PTY's reader ends with EOF or, on macOS and Linux, EIO once the child has exited.
    private static async Task<string> ReadToExitAsync(IPtyConnection pty, CancellationToken ct)
    {
        var text = new StringBuilder();
        var buffer = new byte[4096];
        try
        {
            int read;
            while ((read = await pty.ReaderStream.ReadAsync(buffer, ct)) > 0)
            {
                text.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }
        }
        catch (IOException) { }

        return text.ToString();
    }
}
