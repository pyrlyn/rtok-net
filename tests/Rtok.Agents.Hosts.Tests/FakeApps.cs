using Rtok.Agents.Hosts.Restart;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>
/// A process table in memory (rtok <c>FakeProcs</c>): <see cref="Running"/> holds app and binary
/// names, and <see cref="Calls"/> logs each quit and open in order, beside the writes a test logs.
/// </summary>
internal sealed class FakeApps : IAppProcesses
{
    public HashSet<string> Running { get; } = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    /// <summary>Every name asked whether it runs.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>A quit that cannot be sent.</summary>
    public bool FailQuit { get; init; }

    /// <summary>A quit that is sent, but the app stays.</summary>
    public bool StuckQuit { get; init; }

    /// <summary>An app that cannot be started again.</summary>
    public bool FailOpen { get; init; }

    public bool AppRunning(string name)
    {
        Asked.Add(name);
        return Running.Contains(name);
    }

    public bool BinRunning(string bin) => Running.Contains(bin);

    public void Quit(string name)
    {
        Calls.Add($"quit:{name}");
        if (FailQuit)
        {
            throw new AgentsException("boom");
        }
        if (!StuckQuit)
        {
            Running.Remove(name);
        }
    }

    public void Open(string name, string path)
    {
        Calls.Add($"open:{name}");
        if (FailOpen)
        {
            throw new AgentsException($"open -a {name}: exit status: 1");
        }
    }
}
