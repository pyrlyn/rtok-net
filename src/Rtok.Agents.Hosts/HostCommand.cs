namespace Rtok.Agents.Hosts;

/// <summary>
/// Runs <paramref name="program"/> with <paramref name="args"/> and stdin closed, for at most
/// <paramref name="timeout"/>; null when it cannot start or runs out of time.
/// </summary>
/// <returns>What it printed on stdout and stderr.</returns>
public delegate (string Stdout, string Stderr)? HostCommand(
    string program,
    IReadOnlyList<string> args,
    TimeSpan timeout
);
