using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Docs;

/// <summary>
/// The docs plugin: dependency docs at their lockfile-pinned versions, fetched once and searched
/// offline (rtok T455). Off by default. The tools' work is <see cref="DocsService"/>; this type
/// is what the registry lists.
/// </summary>
public sealed class DocsPlugin : IPlugin
{
    /// <summary>The plugin id: its <c>[plugins.docs]</c> section and its Measurement rows.</summary>
    public const string Id = "docs";

    /// <inheritdoc/>
    public Manifest Manifest { get; } = new(Id, Surface.Mcp | Surface.Cli, DefaultOn: false);

    /// <inheritdoc/>
    public DashboardPage DashboardPage { get; } =
        new(
            "Docs",
            "Ranked dependency doc snippets from lockfiles, fetched once and searched offline.",
            true
        );

    /// <inheritdoc/>
    public IReadOnlyList<ToolDef> McpTools { get; } =
    [
        new(
            "docs_resolve",
            "Exact package name and version from the lockfile for a dependency name.",
            Schema(
                """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}"""
            )
        ),
        new(
            "docs_query",
            "FTS snippets for one cached package version; ids only, docs_get loads a body.",
            Schema(
                """{"type":"object","properties":{"name":{"type":"string"},"version":{"type":"string"},"query":{"type":"string"}},"required":["name","query"]}"""
            )
        ),
        new(
            "docs_get",
            "One cached doc item by id.",
            Schema("""{"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}""")
        ),
    ];

    /// <summary>The plugin's settings for one dispatch.</summary>
    public static DocsConfig Config(Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(cx);
        return cx.PluginConfig(Id, DocsJsonContext.Default.DocsConfig);
    }

    private static JsonElement Schema(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
