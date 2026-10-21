namespace Rtok.Config;

/// <summary>Reasonix home (`REASONIX_HOME`, `%APPDATA%\reasonix` on Windows): `config.toml` holds the `[[plugins]]` MCP servers</summary>
/// <remarks>Read from [setup.reasonix].</remarks>
public sealed class SetupReasonixSection
{
    /// <summary>Reasonix: config.toml ([[plugins]] MCP servers); REASONIX_HOME moves it (Windows: %APPDATA%/reasonix)</summary>
    public string Dir { get; set; } = "~/.reasonix";
}
