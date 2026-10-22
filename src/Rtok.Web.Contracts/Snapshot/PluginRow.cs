namespace Rtok.Web.Contracts;

/// <summary>One linked plugin: whether it runs, where, and its dashboard page.</summary>
/// <param name="Kind"><c>builtin</c> or <c>external</c>.</param>
public sealed record PluginRow(
    string Id,
    string Kind,
    bool Enabled,
    IReadOnlyList<string> Surfaces,
    string Title,
    string Summary,
    bool SavesTokens,
    IReadOnlyList<PluginField> Fields
);
