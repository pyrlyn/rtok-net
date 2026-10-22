namespace Rtok.Archive;

/// <summary>What to expand: an archive id or its 8+ hex prefix, and the optional
/// views.</summary>
/// <param name="Id">The id from an <c>expand &lt;id&gt;</c> trailer.</param>
/// <param name="Lines"><c>--lines a-b</c>, as the caller spelled it.</param>
/// <param name="Grep"><c>--grep</c>: a regex, or a literal when it does not compile.</param>
/// <param name="Context"><c>--context N</c>: lines around each grep hit.</param>
public sealed record ExpandRequest(
    string Id,
    string? Lines = null,
    string? Grep = null,
    int Context = 0
);
