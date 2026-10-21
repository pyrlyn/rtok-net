namespace Rtok.Config;

/// <summary>Grok Build's `config.toml`</summary>
/// <remarks>Read from [setup.grok].</remarks>
public sealed class SetupGrokSection
{
    /// <summary>Grok Build's `config.toml`</summary>
    public string ConfigPath { get; set; } = "~/.grok/config.toml";
}
