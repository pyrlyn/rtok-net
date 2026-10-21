namespace Rtok.Plugin.Sdk;

/// <summary>
/// The page a plugin contributes to the operator surfaces; <c>rtok web</c> and <c>rtok tui</c>
/// render the same one (rtok D23). The host attaches the stats from Measurement rows.
/// </summary>
/// <param name="Title">Short human title, e.g. <c>Bash / cmd</c>.</param>
/// <param name="Summary">One sentence on what the plugin does for the operator.</param>
/// <param name="SavesTokens">Whether the shared stats widget applies: a plugin with no
/// <see cref="Measurement"/> path shows none rather than a row of zeroes.</param>
public sealed record DashboardPage(string Title, string Summary, bool SavesTokens)
{
    /// <summary>Extra key/value rows, typically the plugin's effective configuration.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; init; } = [];
}
