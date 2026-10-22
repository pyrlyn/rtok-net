using System.ComponentModel;
using System.Text;
using Rtok.Mcp.Install;
using Rtok.Process;

namespace Rtok.Agents.Hosts.Restart;

/// <summary>
/// The machine's process table through ProgressRunner (rtok <c>RealProcs</c>): <c>osascript</c>
/// and <c>open -a</c> on macOS, <c>tasklist</c>, <c>taskkill</c> and <c>start</c> on Windows,
/// <c>pgrep -x</c>, <c>killall</c> and a detached start on Linux.
/// </summary>
/// <param name="os">Whose tools to run.</param>
public sealed class AppProcesses(McpOs os) : IAppProcesses
{
    /// <summary>How long one probe, quit or open may run before it counts as failed.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public bool AppRunning(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return os switch
        {
            McpOs.MacOS => Probe("osascript", ["-e", $"application \"{name}\" is running"]) is { } r
                && string.Equals(Text(r.Stdout).Trim(), "true", StringComparison.Ordinal),
            McpOs.Windows => Listed(name),
            _ => Probe("pgrep", ["-x", name]) is { ExitCode: 0 },
        };
    }

    /// <inheritdoc/>
    /// <remarks>Never <c>osascript</c>: its name lookup ignores case and can match another app.</remarks>
    public bool BinRunning(string bin)
    {
        ArgumentNullException.ThrowIfNull(bin);
        return os == McpOs.Windows ? Listed(bin) : Probe("pgrep", ["-x", bin]) is { ExitCode: 0 };
    }

    /// <inheritdoc/>
    /// <remarks>macOS asks the app to quit; Windows and Linux end it (<c>taskkill /F</c>, <c>killall</c>).</remarks>
    public void Quit(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Equals("rtok", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var label = $"closing {name}…";
        switch (os)
        {
            case McpOs.MacOS:
                Check(
                    new ProgressRunner(
                        label,
                        "osascript",
                        ["-e", $"tell application \"{name}\" to quit"]
                    ),
                    $"osascript quit {name}"
                );
                break;
            case McpOs.Windows:
                Check(
                    new ProgressRunner(label, "taskkill", ["/IM", ImageName(name), "/F"]),
                    $"taskkill {name}"
                );
                break;
            default:
                Check(new ProgressRunner(label, "killall", [name]), $"killall {name}");
                break;
        }
    }

    /// <inheritdoc/>
    public void Open(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);
        var label = $"reopening {name}…";
        switch (os)
        {
            case McpOs.MacOS:
                Check(new ProgressRunner(label, "open", ["-a", name]), $"open -a {name}");
                break;
            case McpOs.Windows:
                Check(
                    new ProgressRunner(
                        label,
                        Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                        ["/C", "start", "", path]
                    ),
                    $"start {name}"
                );
                break;
            default:
                // The app keeps running after rtok exits, so nobody waits for it.
                var runner = File.Exists(path)
                    ? ProgressRunner.Quiet(path, [])
                    : ProgressRunner.Quiet("xdg-open", [name]);
                try
                {
                    runner.Output(OutputMode.Detached).Spawn().Dispose();
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException)
                {
                    throw new AgentsException($"start {name}: {e.Message}", e);
                }
                break;
        }
    }

    /// <summary><paramref name="name"/> as <c>tasklist</c> and <c>taskkill</c> name it: <c>.exe</c> added once.</summary>
    internal static string ImageName(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";

    /// <summary>Runs <paramref name="runner"/> to the end; a start failure or a non-zero exit throws, named <paramref name="what"/>.</summary>
    /// <exception cref="AgentsException">It did not start, ran out of time or exited non-zero.</exception>
    internal static void Check(ProgressRunner runner, string what)
    {
        ProcessResult result;
        try
        {
            result = runner.Timeout(Limit).RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception e)
            when (e is Win32Exception or TimeoutException or InvalidOperationException)
        {
            throw new AgentsException($"{what}: {e.Message}", e);
        }
        if (result.ExitCode != 0)
        {
            throw new AgentsException(
                $"{what}: exit status: {result.ExitCode} {Text(result.Stderr).Trim()}".TrimEnd()
            );
        }
    }

    private static bool Listed(string name)
    {
        var exe = ImageName(name);
        return Probe("tasklist", ["/FI", $"IMAGENAME eq {exe}"]) is { } r
            && Text(r.Stdout).Contains(exe, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A quick read with no indicator; null when it cannot start or runs out of time.</summary>
    private static ProcessResult? Probe(string program, IEnumerable<string> args)
    {
        try
        {
            return ProgressRunner
                .Quiet(program, args)
                .Timeout(Limit)
                .RunAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception e)
            when (e is Win32Exception or TimeoutException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
