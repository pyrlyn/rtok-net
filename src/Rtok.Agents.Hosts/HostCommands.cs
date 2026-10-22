using System.ComponentModel;
using System.Text;
using Rtok.Process;

namespace Rtok.Agents.Hosts;

/// <summary>The real <see cref="HostCommand"/>, on ProgressRunner.</summary>
public static class HostCommands
{
    /// <summary>
    /// Runs the program with no indicator; a Windows <c>.cmd</c> or <c>.bat</c> shim (npm-installed
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
        try
        {
            var result = ProgressRunner
                .Quiet(program, args)
                .WindowsShim()
                .Timeout(timeout)
                .RunAsync()
                .GetAwaiter()
                .GetResult();
            return (Encoding.UTF8.GetString(result.Stdout), Encoding.UTF8.GetString(result.Stderr));
        }
        catch (Exception e)
            when (e is TimeoutException or Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
