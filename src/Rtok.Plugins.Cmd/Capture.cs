using System.Text;
using Rtok.Process;

namespace Rtok.Plugins.Cmd;

/// <summary>Runs a shell body and keeps its stdout then stderr, bounded.</summary>
internal static class Capture
{
    /// <summary>
    /// Most bytes of stdout and stderr one <c>rtok run</c> keeps. The host shows an agent
    /// ~30 000 chars, so this only has to stop a <c>cat huge.log</c> from taking gigabytes.
    /// </summary>
    public const long Cap = 32 * 1024 * 1024;

    /// <summary>
    /// Runs <paramref name="body"/> in <paramref name="shell"/> on a closed stdin and returns
    /// stdout then stderr, at most <paramref name="cap"/> bytes together, and the exit code
    /// (<c>128 + signal</c> for a signal death). Bytes past the cap are read and counted, so the
    /// child never blocks on a full pipe; a capped body ends with a notice line.
    /// </summary>
    public static async Task<(byte[] Body, int Exit)> RunAsync(
        string shell,
        string body,
        string label,
        long cap,
        CancellationToken ct
    )
    {
        var args = Shells.Args(shell, body);
        // ProgressRunner would re-quote a cmd.exe body with MSVC rules (`\"`), which cmd.exe
        // does not parse; that body goes onto the command line as is.
        var runner =
            Shells.Kind(shell) == ShellKind.Cmd
                ? new ProgressRunner(label, shell, args[..^1]).RawArgument(body)
                : new ProgressRunner(label, shell, args);
        var result = await runner
            .Output(OutputMode.Capture)
            .RunCappedAsync(cap, ct)
            .ConfigureAwait(false);
        return (Join(result, cap), result.ExitCode);
    }

    /// <summary>The spinner text: <c>running</c> and the command's first line, 60 characters at most.</summary>
    public static string Label(IReadOnlyList<string> command)
    {
        var text = string.Join(' ', command);
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        var line = newline < 0 ? text : text[..newline];
        return $"running {Text.TakeScalars(line, 60)}…";
    }

    /// <summary>Stdout then stderr, and the notice when the cap dropped bytes.</summary>
    public static byte[] Join(ProcessResult result, long cap)
    {
        var body = new MemoryStream(result.Stdout.Length + result.Stderr.Length);
        body.Write(result.Stdout);
        body.Write(result.Stderr);
        if (result.Dropped > 0)
        {
            if (body.Length > 0 && body.GetBuffer()[body.Length - 1] != (byte)'\n')
            {
                body.WriteByte((byte)'\n');
            }

            var size = cap % (1024 * 1024) == 0 ? $"{cap / (1024 * 1024)} MiB" : $"{cap} bytes";
            body.Write(
                Encoding.UTF8.GetBytes(
                    $"[rtok: output capped at {size}; {result.Dropped} bytes not captured]\n"
                )
            );
        }

        return body.ToArray();
    }
}
