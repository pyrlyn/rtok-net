using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>What host probes and paths depend on, so a test can build a fake home on any OS.</summary>
/// <param name="Home">The user's home directory (<c>HOME</c>, else <c>USERPROFILE</c>).</param>
/// <param name="Os">Whose path rules and app layout to follow.</param>
/// <param name="Variable">Reads an environment variable; null when unset.</param>
public sealed record HostEnvironment(string Home, McpOs Os, Func<string, string?> Variable)
{
    /// <summary>
    /// Set (to any value) by the test harness: no test may find or run an agent installed on the
    /// machine. Hosts are then looked up only under the home directory: <c>PATH</c> entries below
    /// it, and app paths re-rooted beneath it (rtok T280).
    /// </summary>
    public const string SandboxVariable = "RTOK_HOST_SANDBOX";

    /// <summary>Whether <see cref="SandboxVariable"/> is set.</summary>
    public bool Sandboxed => Variable(SandboxVariable) is not null;

    /// <summary>The machine's own environment.</summary>
    public static HostEnvironment Current()
    {
        var home =
            Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } h ? h
            : Environment.GetEnvironmentVariable("USERPROFILE") is { Length: > 0 } p ? p
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var os =
            OperatingSystem.IsWindows() ? McpOs.Windows
            : OperatingSystem.IsMacOS() ? McpOs.MacOS
            : McpOs.Linux;
        return new HostEnvironment(home, os, Environment.GetEnvironmentVariable);
    }
}
