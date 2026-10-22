using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Docs;

/// <summary>
/// The docs plugin: dependency docs at their lockfile-pinned versions, fetched once and searched
/// offline (rtok T455). Off by default. The tools' work is <see cref="DocsService"/>; this type
/// is what the registry lists.
/// </summary>
/// <param name="service">The host's service for a dispatch: its store, the settings from
/// <see cref="Config"/> and rtok's home. Without one a docs tool call fails as a tool error.</param>
public sealed class DocsPlugin(Func<Ctx, DocsService>? service = null) : IPlugin
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

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">A required argument is missing.</exception>
    /// <exception cref="InvalidOperationException">The host gave this plugin no service.</exception>
    public string? CallTool(string name, JsonElement arguments, Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(cx);
        if (name is not ("docs_resolve" or "docs_query" or "docs_get"))
        {
            return null;
        }

        var docs =
            service?.Invoke(cx)
            ?? throw new InvalidOperationException("docs: this host has no docs store");
        var cwd = cx.Host.Cwd ?? Environment.CurrentDirectory;
        return name switch
        {
            "docs_resolve" => docs.Resolve(Required(arguments, "name"), cwd),
            "docs_query" => docs.Query(
                Required(arguments, "name"),
                Str(arguments, "version") is { Length: > 0 } version ? version : null,
                Required(arguments, "query"),
                cwd,
                cx
            ),
            _ => docs.Get(
                arguments.ValueKind is JsonValueKind.Object
                && arguments.TryGetProperty("id", out var id)
                && id.ValueKind is JsonValueKind.Number
                && id.TryGetInt64(out var n)
                    ? n
                    : throw new ArgumentException("missing `id`", nameof(arguments))
            ),
        };
    }

    private static string Required(JsonElement args, string key) =>
        Str(args, key) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"missing `{key}`", nameof(args));

    private static string Str(JsonElement args, string key) =>
        args.ValueKind is JsonValueKind.Object
        && args.TryGetProperty(key, out var v)
        && v.ValueKind is JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static JsonElement Schema(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
