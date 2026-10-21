namespace Rtok.Mcp.Install;

/// <summary>Which client of a host an entry is for; Claude Code and Claude Desktop are two clients of one host.</summary>
public enum McpClient
{
    /// <summary>The host's command-line agent.</summary>
    Cli,

    /// <summary>The host's desktop app.</summary>
    Desktop,

    /// <summary>An editor extension.</summary>
    Ide,
}
