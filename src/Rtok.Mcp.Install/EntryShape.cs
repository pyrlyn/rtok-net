namespace Rtok.Mcp.Install;

/// <summary>How the entry under the server's name is spelled; each host's schema takes one.</summary>
public enum EntryShape
{
    /// <summary><c>{"type": "stdio", "command": …, "args": […]}</c>.</summary>
    TypedCommand,

    /// <summary><c>{"command": …, "args": […]}</c>: the host has only one transport.</summary>
    Command,

    /// <summary><c>{"type": "local", "command": …, "args": […], "tools": ["*"]}</c> (Copilot CLI).</summary>
    LocalCommand,

    /// <summary><c>{"type": "local", "command": [argv…], "enabled": true}</c> (OpenCode, Kilo, MiMo).</summary>
    LocalArgv,

    /// <summary><c>command</c> and <c>args</c> plus <c>default_tools_approval_mode = "writes"</c> (Codex).</summary>
    CommandWritesApproved,
}
