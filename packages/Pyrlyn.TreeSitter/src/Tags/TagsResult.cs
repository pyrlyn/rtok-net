namespace Pyrlyn.TreeSitter.Tags;

/// <summary>The tags of one source buffer.</summary>
/// <param name="Tags">The tags in order of their names' end, then start.</param>
/// <param name="HasError">True when the parse tree contains an error or missing node.</param>
public sealed record TagsResult(IReadOnlyList<Tag> Tags, bool HasError);
