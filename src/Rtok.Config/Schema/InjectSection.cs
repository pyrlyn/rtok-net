namespace Rtok.Config;

/// <summary>modes and context injected at session start</summary>
/// <remarks>Read from [plugins.inject].</remarks>
public sealed class InjectSection
{
    /// <summary>inject modes and context at session start</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>per turn, all injections together</summary>
    public uint BudgetTokens { get; set; } = 800;

    /// <summary>where mode files live</summary>
    public string ModesDir { get; set; } = "~/.rtok/modes";

    /// <summary>same as [setup].modes; setup writes here</summary>
    public IList<string> Modes { get; set; } = [];
}
