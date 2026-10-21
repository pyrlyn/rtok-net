namespace Rtok.Model;

/// <summary>Where a reading command shows on each surface.</summary>
/// <param name="Web">The web dashboard page.</param>
/// <param name="Tui">The TUI tab.</param>
public sealed record PageLink(Page Web, Page Tui);
