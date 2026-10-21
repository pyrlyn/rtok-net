namespace Rtok.Config;

/// <summary>Gemini CLI's `settings.json` (`hooks`, `mcpServers`) lives under `dir`</summary>
/// <remarks>Read from [setup.gemini].</remarks>
public sealed class SetupGeminiSection
{
    /// <summary>settings.json (hooks, mcpServers)</summary>
    public string Dir { get; set; } = "~/.gemini";
}
