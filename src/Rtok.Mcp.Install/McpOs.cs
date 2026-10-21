namespace Rtok.Mcp.Install;

/// <summary>The OS whose app-data layout a host table follows.</summary>
public enum McpOs
{
    /// <summary>Linux and other Unix: <c>~/.config</c>.</summary>
    Linux,

    /// <summary>macOS: <c>~/Library/Application Support</c>.</summary>
    MacOS,

    /// <summary>Windows: <c>%APPDATA%</c>.</summary>
    Windows,
}
