namespace Rtok.Config;

/// <summary>`kilo.json`, merged by Kilo with a user's `kilo.jsonc`</summary>
/// <remarks>Read from [setup.kilo].</remarks>
public sealed class SetupKiloSection
{
    /// <summary>kilo.jsonc is merged by Kilo, never rewritten</summary>
    public string ConfigPath { get; set; } = "~/.config/kilo/kilo.json";
}
