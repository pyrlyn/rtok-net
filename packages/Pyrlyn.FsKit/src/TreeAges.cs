namespace Pyrlyn.FsKit;

/// <summary>
/// When a tree was last used, by its files' mtimes (a directory's own mtime is not use), and the
/// <c>CACHEDIR.TAG</c> caches inside it.
/// </summary>
/// <param name="Newest">The newest file mtime; null for a tree without files.</param>
/// <param name="Incomplete">
/// A directory, entry or mtime could not be read: <paramref name="Newest"/> is a lower bound,
/// since the newest file may be the one the walk missed. A path gone mid-walk is not missing.
/// </param>
/// <param name="TimedOut">The walk stopped at its time limit, so every answer is a lower bound.</param>
/// <param name="Caches">Every tagged cache root, outermost only, in walk order.</param>
public sealed record TreeAges(
    DateTime? Newest,
    bool Incomplete,
    bool TimedOut,
    IReadOnlyList<TaggedCache> Caches
);
