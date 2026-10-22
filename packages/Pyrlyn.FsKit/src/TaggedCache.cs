namespace Pyrlyn.FsKit;

/// <summary>One <c>CACHEDIR.TAG</c> root found by <see cref="FsTree.Ages"/>.</summary>
/// <param name="Path">The directory holding the tag.</param>
/// <param name="Newest">The newest file mtime inside it, the tag included.</param>
/// <param name="Incomplete">Something inside it could not be read.</param>
public sealed record TaggedCache(string Path, DateTime? Newest, bool Incomplete);
