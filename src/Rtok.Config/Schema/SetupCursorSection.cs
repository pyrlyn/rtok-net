namespace Rtok.Config;

/// <summary>Cursor's user `hooks.json`</summary>
/// <remarks>Read from [setup.cursor].</remarks>
public sealed class SetupCursorSection
{
    /// <summary>rtok's hook entries go here</summary>
    public string HooksPath { get; set; } = "~/.cursor/hooks.json";
}
