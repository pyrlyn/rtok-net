namespace Rtok.Agents.Hosts.Restart;

/// <summary>A process table with nothing running: a sandboxed run, and every context a test builds without one.</summary>
internal sealed class NoAppProcesses : IAppProcesses
{
    public static readonly NoAppProcesses Instance = new();

    public bool AppRunning(string name) => false;

    public bool BinRunning(string bin) => false;

    public void Quit(string name) { }

    public void Open(string name, string path) { }
}
