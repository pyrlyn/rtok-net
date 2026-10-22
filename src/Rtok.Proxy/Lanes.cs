using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>What a lane means: its name, its <c>calls.kind</c>, its policy and its routing rule,
/// and how a request is put on one.</summary>
public static class Lanes
{
    /// <summary>The marker header; read by the proxy and never forwarded.</summary>
    public const string Header = "x-rtok-lane";

    /// <summary>The path prefix marker: <c>/lane/&lt;name&gt;/rest</c> forwards <c>/rest</c>.</summary>
    public const string Prefix = "/lane/";

    /// <summary>The <c>calls.kind</c> of the agent lane; every other lane appends <c>:name</c>.</summary>
    public const string AgentKind = "api_request";

    private const string KindPrefix = AgentKind + ":";

    private static readonly string[] BatchRoots =
    [
        "/v1/messages/batches",
        "/v1/batches",
        "/v1beta/batches",
    ];

    private static readonly string[] FilesRoots =
    [
        "/v1/files",
        "/v1beta/files",
        "/upload/v1beta/files",
    ];

    private static readonly string[] MetaRoots =
    [
        "/v1/messages/count_tokens",
        "/v1/models",
        "/v1beta/models",
    ];

    /// <summary>Every lane, agent first.</summary>
    public static IReadOnlyList<Lane> All { get; } = Enum.GetValues<Lane>();

    /// <summary>The lane's lowercase name, as config tables and markers spell it.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>The name.</returns>
    public static string Name(this Lane lane) =>
        lane switch
        {
            Lane.Agent => "agent",
            Lane.Bulk => "bulk",
            Lane.Batch => "batch",
            Lane.Files => "files",
            Lane.Embeddings => "embeddings",
            Lane.Meta => "meta",
            _ => "internal",
        };

    /// <summary>The lane a marker names, ignoring case and surrounding space.</summary>
    /// <param name="name">The marker value.</param>
    /// <returns>The lane, or null for a name no lane has.</returns>
    public static Lane? Parse(string? name)
    {
        var trimmed = name?.Trim();
        foreach (var lane in All)
        {
            if (string.Equals(lane.Name(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return lane;
            }
        }
        return null;
    }

    /// <summary>The <c>calls.kind</c>: <c>api_request</c> for the agent lane, else
    /// <c>api_request:&lt;lane&gt;</c>.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>The kind.</returns>
    public static string Kind(this Lane lane) =>
        lane == Lane.Agent ? AgentKind : KindPrefix + lane.Name();

    /// <summary>Whether a <c>calls.kind</c> is a proxied API request, on any lane.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True for <c>api_request</c> and <c>api_request:*</c>.</returns>
    public static bool IsApiRequest(string kind) =>
        string.Equals(kind, AgentKind, StringComparison.Ordinal)
        || kind.StartsWith(KindPrefix, StringComparison.Ordinal);

    /// <summary>The lane name a <c>calls.kind</c> carries; <c>agent</c> without one.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The lane name.</returns>
    public static string NameOfKind(string kind) =>
        kind.StartsWith(KindPrefix, StringComparison.Ordinal) ? kind[KindPrefix.Length..] : "agent";

    /// <summary>Batch and files traffic is forwarded as sent: no rewrite, not even the wire's own
    /// shaping.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>True for batch and files.</returns>
    public static bool PassesThrough(this Lane lane) => lane is Lane.Batch or Lane.Files;

    /// <summary>The lane's own table. The agent lane opens every switch (the global ones decide),
    /// batch and files open none and ignore the config, the rest read
    /// <c>[proxy.lanes.&lt;lane&gt;]</c>.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="lanes"><c>[proxy.lanes]</c>.</param>
    /// <returns>The policy.</returns>
    public static LanePolicy Policy(this Lane lane, LanesSection lanes) =>
        lane switch
        {
            Lane.Agent => new LanePolicy
            {
                Compress = true,
                Toon = true,
                ToolsRewrite = true,
                ContextManagement = true,
                SemanticCache = true,
                MaxQueued = 0,
            },
            Lane.Bulk => lanes.Bulk,
            Lane.Embeddings => lanes.Embeddings,
            Lane.Meta => lanes.Meta,
            Lane.Internal => lanes.Internal,
            _ => new LanePolicy(),
        };

    /// <summary>The routing rule for the lane: only bulk and internal have one, and only while
    /// <c>[proxy.routing] enabled</c>.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="routing"><c>[proxy.routing]</c>.</param>
    /// <returns>The rule, or null.</returns>
    public static RouteRule? Route(this Lane lane, RoutingPolicy routing) =>
        !routing.Enabled ? null
        : lane == Lane.Bulk ? routing.Bulk
        : lane == Lane.Internal ? routing.Internal
        : null;

    /// <summary>The lane of a request and the path to forward. The path decides first (a batch
    /// path is batch whatever the marker says), then the <c>/lane/&lt;name&gt;/</c> prefix, then
    /// the header; nothing makes it agent. The prefix is cut from the path; an unknown name, or a
    /// prefix with nothing after the name, leaves the path as sent.</summary>
    /// <param name="path">The request path, still percent-encoded.</param>
    /// <param name="header">The <c>x-rtok-lane</c> value, if any.</param>
    /// <returns>The lane and the path to forward.</returns>
    public static (Lane Lane, string Path) Classify(string path, string? header)
    {
        var (prefixed, forwarded) = StripPrefix(path);
        var marked = prefixed ?? Parse(header);
        return (ByPath(forwarded) ?? marked ?? Lane.Agent, forwarded);
    }

    private static (Lane? Lane, string Path) StripPrefix(string path)
    {
        if (!path.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return (null, path);
        }
        var slash = path.IndexOf('/', Prefix.Length);
        return slash >= 0 && Parse(path[Prefix.Length..slash]) is { } lane
            ? (lane, path[slash..])
            : (null, path);
    }

    private static Lane? ByPath(string path)
    {
        if (Under(path, "/v1beta/models") || Under(path, "/v1/models"))
        {
            var colon = path.LastIndexOf(':');
            if (colon >= 0)
            {
                return path[(colon + 1)..] switch
                {
                    "batchGenerateContent" or "asyncBatchEmbedContent" => Lane.Batch,
                    "embedContent" or "batchEmbedContents" => Lane.Embeddings,
                    "countTokens" => Lane.Meta,
                    _ => null,
                };
            }
        }
        return Array.Exists(BatchRoots, r => Under(path, r)) ? Lane.Batch
            : Array.Exists(FilesRoots, r => Under(path, r)) ? Lane.Files
            : Under(path, "/v1/embeddings") ? Lane.Embeddings
            : Array.Exists(MetaRoots, r => Under(path, r)) ? Lane.Meta
            : null;
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or below it.</summary>
    internal static bool Under(string path, string root) =>
        path.StartsWith(root, StringComparison.Ordinal)
        && (path.Length == root.Length || path[root.Length] == '/');
}
