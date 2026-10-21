namespace Rtok.Config;

/// <summary>`.aider.conf.yml` carries `openai-api-base`</summary>
/// <remarks>Read from [setup.aider].</remarks>
public sealed class SetupAiderSection
{
    /// <summary>openai-api-base → rtok proxy (--proxy)</summary>
    public string ConfigPath { get; set; } = "~/.aider.conf.yml";
}
