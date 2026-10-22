namespace Rtok.Plugins.Graph;

/// <summary>One watcher notice: a changed path, or <see cref="Rescan"/> when the OS lost
/// events and only a full walk can tell what changed.</summary>
internal readonly record struct WatchEvent(string? Path, bool Rescan = false);
