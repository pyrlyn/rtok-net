using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Rtok.Agents.Hosts.Restart;

/// <summary>
/// Closes a running desktop app before rtok writes its config and reopens it afterwards, since the
/// app reads its config only at start (rtok <c>restart.rs</c>, T141, T434, T629). Closing and
/// reopening never fail a run: a problem is a warning to restart the app by hand.
/// </summary>
internal sealed class DesktopRestart
{
    /// <summary>How often a closing app is asked whether it still runs.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

    private readonly HostContext ctx;
    private readonly List<Step> plan;
    private readonly List<(string Name, string Path)> reopen = [];

    private DesktopRestart(HostContext ctx, List<Step> plan)
    {
        this.ctx = ctx;
        this.plan = plan;
    }

    /// <summary>
    /// Plans every host before any app is closed: <paramref name="wouldChange"/> (a dry run of the
    /// host) runs for each host with a running app or CLI, and a failing one throws while every
    /// app still runs. A host whose config would stay as it is is left alone.
    /// </summary>
    public static DesktopRestart Plan(
        HostContext ctx,
        IEnumerable<AgentHost> hosts,
        Func<AgentHost, bool> wouldChange
    )
    {
        var steps = new List<Step>();
        foreach (var host in hosts)
        {
            var app = RunningApp(ctx, host);
            var bin = RunningBin(ctx, host);
            if ((app is not null || bin is not null) && wouldChange(host))
            {
                steps.Add(new Step(host.Id, app, bin));
            }
        }
        return new DesktopRestart(ctx, steps);
    }

    /// <summary>Nothing to close or reopen.</summary>
    public static DesktopRestart None(HostContext ctx) => new(ctx, []);

    /// <summary>
    /// Closes each planned app and waits for it to go; a dry run only says it would. Returns the
    /// lines for stdout: the dry run's and the CLI sessions to restart.
    /// </summary>
    public string Close()
    {
        var text = new StringBuilder();
        foreach (var step in plan)
        {
            if (step.App is var (name, path))
            {
                if (ctx.Settings.DryRun)
                {
                    text.Append(
                        CultureInfo.InvariantCulture,
                        $"{step.Host}: would close/reopen {name}\n"
                    );
                }
                else
                {
                    Quit(name, path);
                }
            }
            if (step.Bin is { } bin)
            {
                text.Append(
                    CultureInfo.InvariantCulture,
                    $"{step.Host}: restart your {bin} session to load the new config\n"
                );
            }
        }
        return text.ToString();
    }

    /// <summary>Starts every app <see cref="Close"/> closed again, whether or not the write went through.</summary>
    public void Reopen()
    {
        foreach (var (name, path) in reopen)
        {
            try
            {
                ctx.Apps.Open(name, path);
            }
            catch (AgentsException e)
            {
                Warn($"could not reopen {name}: {e.Message}; open it manually");
            }
        }
    }

    /// <summary>The name an app runs under: its bundle or binary name, without <c>.app</c>.</summary>
    internal static string AppName(string path, string fallback)
    {
        var file = path.Split('/', '\\').LastOrDefault(p => p.Length > 0);
        return file is null ? fallback
            : file.EndsWith(".app", StringComparison.Ordinal) ? file[..^4]
            : file;
    }

    /// <summary>
    /// The first installed bundle of a desktop app that runs. An app that is not installed is
    /// never asked: <c>osascript</c> may pop up "Choose Application" for an unknown name.
    /// </summary>
    private static (string Name, string Path)? RunningApp(HostContext ctx, AgentHost host)
    {
        foreach (var v in host.Variants.Where(v => v.Kind == HostKind.Desktop))
        {
            foreach (var path in ctx.Probe.InstalledApps(v))
            {
                var name = AppName(path, v.Name);
                if (ctx.Apps.AppRunning(name))
                {
                    return (name, path);
                }
            }
        }
        return null;
    }

    /// <summary>The CLI's first binary that runs: never closed, only named.</summary>
    private static string? RunningBin(HostContext ctx, AgentHost host) =>
        host
            .Variants.FirstOrDefault(v => v.Kind == HostKind.Cli)
            ?.Bins.FirstOrDefault(ctx.Apps.BinRunning);

    private void Quit(string name, string path)
    {
        try
        {
            ctx.Apps.Quit(name);
        }
        catch (AgentsException e)
        {
            Warn($"could not quit {name}: {e.Message}; restart it manually to load the new config");
            return;
        }
        if (Closed(name))
        {
            reopen.Add((name, path));
            return;
        }
        // Likely waiting on its own quit prompt: reopening would only bring it to the front.
        var wait = ctx.QuitWait.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);
        Warn($"{name} did not quit within {wait} s; restart it manually to load the new config");
    }

    /// <summary>Whether the app is gone within <see cref="HostContext.QuitWait"/>; Electron apps often take well over 5 s.</summary>
    private bool Closed(string name)
    {
        var started = Stopwatch.GetTimestamp();
        while (ctx.Apps.AppRunning(name))
        {
            if (Stopwatch.GetElapsedTime(started) >= ctx.QuitWait)
            {
                return false;
            }
            Thread.Sleep(Poll);
        }
        return true;
    }

    private void Warn(string message) => ctx.Settings.Warn?.Invoke("warning: " + message);

    /// <summary>One host to restart: its running app, if any, and its running CLI, if any.</summary>
    private sealed record Step(string Host, (string Name, string Path)? App, string? Bin);
}
