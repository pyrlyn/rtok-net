using CliWrap;
using CliWrap.Buffered;

namespace Rtok.Agents.Hosts;

/// <summary>The real <see cref="HostCommand"/>, until T11's ProgressRunner owns every process.</summary>
public static class HostCommands
{
    /// <summary>
    /// Runs the program through CliWrap; a Windows <c>.cmd</c> or <c>.bat</c> shim (npm-installed
    /// CLIs) goes through <c>cmd.exe</c>.
    /// </summary>
    public static (string Stdout, string Stderr)? Run(
        string program,
        IReadOnlyList<string> args,
        TimeSpan timeout
    )
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(args);
        var shim =
            OperatingSystem.IsWindows()
            && Path.GetExtension(program).ToUpperInvariant() is ".CMD" or ".BAT";
        var command = shim
            ? Cli.Wrap(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
                .WithArguments(["/d", "/s", "/c", program, .. args])
            : Cli.Wrap(program).WithArguments(args);
        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            var result = command
                .WithValidation(CommandResultValidation.None)
                .WithStandardInputPipe(PipeSource.Null)
                .ExecuteBufferedAsync(cancel.Token)
                .GetAwaiter()
                .GetResult();
            return (result.StandardOutput, result.StandardError);
        }
        catch (Exception e)
            when (e
                    is OperationCanceledException
                        or System.ComponentModel.Win32Exception
                        or InvalidOperationException
            )
        {
            return null;
        }
    }
}
