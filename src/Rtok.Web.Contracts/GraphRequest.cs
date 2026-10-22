namespace Rtok.Web.Contracts;

/// <summary>The inside of one project for the graph page's drill-down; read-only.</summary>
/// <param name="Project">A project id or root, as <c>rtok graph projects</c> names it.</param>
/// <param name="Expand">Files or symbols to open one level deeper.</param>
/// <param name="Focus">The node to centre on.</param>
/// <param name="Depth">How many edges out from the focus.</param>
/// <param name="Limit">The most nodes to return.</param>
/// <param name="Query">A name filter.</param>
public sealed record GraphRequest(
    string Project,
    IReadOnlyList<string>? Expand = null,
    string? Focus = null,
    int? Depth = null,
    int? Limit = null,
    string? Query = null
);
