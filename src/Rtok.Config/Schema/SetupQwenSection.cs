namespace Rtok.Config;

/// <summary>Qwen Code's `settings.json` (`hooks`, `mcpServers`) lives under `dir`</summary>
/// <remarks>Read from [setup.qwen].</remarks>
public sealed class SetupQwenSection
{
    /// <summary>settings.json (hooks, mcpServers); QWEN_HOME moves the directory</summary>
    public string Dir { get; set; } = "~/.qwen";
}
