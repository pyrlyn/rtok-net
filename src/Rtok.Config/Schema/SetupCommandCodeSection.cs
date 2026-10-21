namespace Rtok.Config;

/// <summary>hooks merge into `settings.json`, MCP into `mcp.json`</summary>
/// <remarks>Read from [setup.commandcode].</remarks>
public sealed class SetupCommandCodeSection
{
    /// <summary>settings.json (hooks key), mcp.json</summary>
    public string Dir { get; set; } = "~/.commandcode";
}
