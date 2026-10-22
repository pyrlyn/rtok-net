using Rtok.Web.Contracts;

namespace Rtok.Web.Client.Services;

/// <summary>One snapshot page shown as text.</summary>
/// <param name="Slug">Its address on the dashboard.</param>
/// <param name="Title">The heading and sidebar label.</param>
/// <param name="Icon">The Material Symbols icon name.</param>
/// <param name="Read">Where its text is in a snapshot.</param>
public sealed record TextPageInfo(
    string Slug,
    string Title,
    string Icon,
    Func<Snapshot, TextPage> Read
);
