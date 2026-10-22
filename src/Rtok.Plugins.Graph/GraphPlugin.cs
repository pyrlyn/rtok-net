using System.Text.Json;
using Pyrlyn.FsKit;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The graph plugin: a tree-sitter tags index of the project and five navigation tools on it,
/// which answer "what is this, who uses it, what breaks" far cheaper than reading files. An
/// Edit or Write marks its file stale so the next answer re-indexes it.
/// </summary>
public sealed class GraphPlugin : IPlugin
{
    private static readonly IReadOnlyList<ToolDef> Tools =
    [
        Tool(
            "symbol",
            "Definitions of a symbol — id, path:line kind, then the body — optional id or names; path or kind narrows a name.",
            """{"type":"object","properties":{"name":{"type":"string"},"names":{"type":"array","items":{"type":"string"}},"id":{"type":"string"},"path":{"type":"string"},"kind":{"type":"string"}}}"""
        ),
        Tool(
            "callers",
            "Which definitions reference a symbol: path, caller, count. Optional path keeps one subtree.",
            """{"type":"object","properties":{"name":{"type":"string"},"path":{"type":"string"}},"required":["name"]}"""
        ),
        Tool(
            "impact",
            "What breaks if a symbol changes: callers up to depth, grouped by file and cut at a token budget (all=true: every row). Optional to: chains reaching it.",
            """{"type":"object","properties":{"name":{"type":"string"},"to":{"type":"string"},"depth":{"type":"integer"},"all":{"type":"boolean"},"path":{"type":"string"}},"required":["name"]}"""
        ),
        Tool(
            "outline",
            "Definitions in one file (read mode=map); kind filters.",
            """{"type":"object","properties":{"path":{"type":"string"},"kind":{"type":"string"}},"required":["path"]}"""
        ),
        Tool(
            "explore",
            "Answers a code question: the query's symbols as definitions with bodies, call paths between them, impact counts. Optional path narrows.",
            """{"type":"object","properties":{"query":{"type":"string"},"path":{"type":"string"}},"required":["query"]}"""
        ),
    ];

    /// <inheritdoc/>
    public Manifest Manifest { get; } = new("graph", Surface.Mcp | Surface.Hook, DefaultOn: true);

    /// <inheritdoc/>
    public DashboardPage DashboardPage { get; } =
        new("Graph", "symbol / callers / impact from a tree-sitter-tags index.", SavesTokens: true);

    /// <inheritdoc/>
    public IReadOnlyList<ToolDef> McpTools => Tools;

    /// <inheritdoc/>
    public string? OnPostTool(PostToolUse ev, Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(cx);
        if (
            ev.ToolName is "Edit" or "Write"
            && ev.ToolInput.ValueKind is JsonValueKind.Object
            && ev.ToolInput.TryGetProperty("file_path", out var file)
            && file.ValueKind is JsonValueKind.String
            && file.GetString() is { Length: > 0 } path
        )
        {
            cx.Host.MarkSymbolsStale(FsPath.Canon(Path.GetFullPath(path)));
        }

        return null;
    }

    /// <inheritdoc/>
    public string? CallTool(string name, JsonElement arguments, Ctx cx) =>
        GraphCall.Run(cx, name, arguments);

    private static ToolDef Tool(string name, string description, string schema)
    {
        using var doc = JsonDocument.Parse(schema);
        return new ToolDef(name, description, doc.RootElement.Clone());
    }
}
