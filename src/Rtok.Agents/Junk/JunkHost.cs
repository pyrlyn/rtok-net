namespace Rtok.Agents.Junk;

/// <summary>One host as the junk scan sees it.</summary>
/// <param name="Id">The host id.</param>
/// <param name="Installed">One of its apps is here.</param>
/// <param name="Markers">The config files rtok edits for it (its MCP files): their folders are listed and protected.</param>
/// <param name="ProcessNames">Its binaries and app names, lowercase: what a crash report is named for.</param>
public sealed record JunkHost(
    string Id,
    bool Installed,
    IReadOnlyList<string> Markers,
    IReadOnlyList<string> ProcessNames
);
