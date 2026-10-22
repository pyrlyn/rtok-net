using Rtok.Web.Contracts;

namespace Rtok.Web.Client.Services;

/// <summary>The snapshot pages shown as text, by their address on the dashboard.</summary>
public static class TextPages
{
    /// <summary>Every text page in sidebar order.</summary>
    public static readonly IReadOnlyList<TextPageInfo> All =
    [
        new("doctor", "Doctor", "health_and_safety", s => s.Doctor),
        new("logs", "Logs", "receipt_long", s => s.Logs),
        new("memory", "Memory", "psychology", s => s.Memory),
        new("graph", "Graph", "hub", s => s.Graph),
        new("services", "Services", "dns", s => s.Services),
        new("worktrees", "Worktrees", "account_tree", s => s.Worktrees),
        new("hosts", "Hosts", "smart_toy", s => s.Hosts),
        new("stats", "Stats", "query_stats", s => s.Stats),
        new("usage", "Usage", "data_usage", s => s.Usage),
        new("tasks", "Tasks", "checklist", s => s.Tasks),
    ];

    /// <summary>The text page at <paramref name="slug"/>, or null.</summary>
    /// <param name="slug">The address, such as <c>logs</c>.</param>
    /// <returns>The page.</returns>
    public static TextPageInfo? Find(string slug) =>
        All.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.Ordinal));
}
