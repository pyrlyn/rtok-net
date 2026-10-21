namespace Rtok.Agents.Hosts;

/// <summary>
/// One app of a host. <paramref name="Bins"/> are looked up on <c>PATH</c> and asked
/// <c>--version</c>; <paramref name="Apps"/> are where a desktop build installs (<c>~/…</c>,
/// <c>$VAR/…</c> or absolute), probed for existence. Entries for other platforms never exist.
/// </summary>
/// <param name="Kind">CLI or desktop.</param>
/// <param name="Name">The app's own name: <c>Claude Code</c>, <c>VS Code - Insiders</c>.</param>
/// <param name="Bins">Binary names, in lookup order.</param>
/// <param name="Apps">App bundle or binary paths, in lookup order.</param>
/// <param name="Surface">
/// The MCP surface this app reads (<c>cli</c>, <c>desktop</c>, <c>stable</c>, <c>insiders</c>);
/// null for the kind's word.
/// </param>
public sealed record HostVariant(
    HostKind Kind,
    string Name,
    IReadOnlyList<string> Bins,
    IReadOnlyList<string> Apps,
    string? Surface = null
)
{
    /// <summary>The MCP surface label of this app.</summary>
    public string SurfaceName => Surface ?? Kind.Word();
}
