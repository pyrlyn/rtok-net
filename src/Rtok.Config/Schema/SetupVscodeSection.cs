namespace Rtok.Config;

/// <summary>profile `mcp.json` under Code / Code - Insiders user dirs</summary>
/// <remarks>Read from [setup.vscode].</remarks>
public sealed class SetupVscodeSection
{
    /// <summary>VS Code's user folder; "" = the platform default</summary>
    public string CodeUserDir { get; set; } = "";

    /// <summary>VS Code Insiders' user folder; "" = the platform default</summary>
    public string InsidersUserDir { get; set; } = "";
}
