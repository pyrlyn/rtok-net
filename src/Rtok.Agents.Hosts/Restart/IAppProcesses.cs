namespace Rtok.Agents.Hosts.Restart;

/// <summary>
/// The process table the desktop restart reads and acts on (rtok <c>Procs</c>): a test passes a
/// fake one, so no app on the machine is ever closed.
/// </summary>
public interface IAppProcesses
{
    /// <summary>Whether the desktop app <paramref name="name"/> (<c>Claude</c>, <c>Visual Studio Code</c>) runs.</summary>
    bool AppRunning(string name);

    /// <summary>Whether a process named exactly <paramref name="bin"/> runs.</summary>
    bool BinRunning(string bin);

    /// <summary>Asks the app <paramref name="name"/> to quit; it may still run when this returns.</summary>
    /// <exception cref="AgentsException">The quit could not be sent.</exception>
    void Quit(string name);

    /// <summary>Starts the app <paramref name="name"/> installed at <paramref name="path"/> again.</summary>
    /// <exception cref="AgentsException">The app could not be started.</exception>
    void Open(string name, string path);
}
