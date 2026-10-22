using System.Text;
using System.Text.Json;
using Rtok.Config;
using Rtok.Proxy;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any path and <c>x-rtok-lane</c> value give one lane and a forwarded path with the
/// <c>/lane/&lt;name&gt;/</c> marker cut; the agent lane never has a routing rule, even with every
/// rule active; and any JSON body gives the same cache key twice, for every wire and scope.
/// Input: path, header and body, NUL-separated.
/// </summary>
internal static class LaneClassify
{
    private static readonly string[] Paths =
    [
        "/v1/messages",
        "/v1/chat/completions",
        "/v1/responses",
        "/v1beta/models/gemini-2.5-pro:generateContent",
    ];

    private static readonly RoutingPolicy Routing = new()
    {
        Enabled = true,
        Bulk = Rule(),
        Internal = Rule(),
    };

    private static readonly SemanticCacheSection Cache = new();

    public static void Run(ReadOnlySpan<byte> data)
    {
        var parts = FuzzInput.Parts(data, 3);
        var (path, header, body) = (parts[0], parts[1].Length == 0 ? null : parts[1], parts[2]);
        Classify(path, header);
        Route(path, header, body);
        Keys(path, header, body);
    }

    private static void Classify(string path, string? header)
    {
        var (lane, forwarded) = Lanes.Classify(path, header);
        FuzzInput.Check(Lanes.All.Contains(lane), "a lane outside the enum");
        FuzzInput.Check(
            Lanes.Classify(path, header) == (lane, forwarded),
            "classify is not stable"
        );
        FuzzInput.Check(
            path.EndsWith(forwarded, StringComparison.Ordinal),
            "the forwarded path is not the tail of the sent one"
        );
        if (!string.Equals(forwarded, path, StringComparison.Ordinal))
        {
            var marker = path[..^forwarded.Length];
            FuzzInput.Check(
                forwarded.StartsWith('/')
                    && marker.StartsWith(Lanes.Prefix, StringComparison.Ordinal)
                    && Lanes.Parse(marker[Lanes.Prefix.Length..]) is not null,
                "something other than a known lane marker was cut"
            );
        }
        if (!path.StartsWith(Lanes.Prefix, StringComparison.Ordinal))
        {
            FuzzInput.Check(
                string.Equals(forwarded, path, StringComparison.Ordinal),
                "a path without a marker changed"
            );
        }
    }

    private static void Route(string path, string? header, string body)
    {
        var (lane, _) = Lanes.Classify(path, header);
        var rule = lane.Route(Routing);
        FuzzInput.Check(
            (rule is null) == (lane is not (Lane.Bulk or Lane.Internal)),
            "a rule on a lane other than bulk and internal"
        );
        FuzzInput.Check(Lane.Agent.Route(Routing) is null, "the agent lane has a routing rule");
        if (rule is not null && ModelRoute.Apply(rule, Encoding.UTF8.GetBytes(body)) is { } routed)
        {
            FuzzInput.Check(
                string.Equals(routed.To, rule.To, StringComparison.Ordinal),
                "a route to a model the rule does not name"
            );
        }
    }

    private static void Keys(string path, string? header, string body)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return;
        }
        using (doc)
        {
            IEnumerable<string?> Values(string name) =>
                string.Equals(name, "x-api-key", StringComparison.Ordinal) ? [header] : [];
            var scope = CacheKey.Scope(Values, "POST", path);
            FuzzInput.Check(
                string.Equals(
                    scope,
                    CacheKey.Scope(Values, "POST", path),
                    StringComparison.Ordinal
                ),
                "the cache scope is not deterministic"
            );
            foreach (var wirePath in Paths)
            {
                var wire = Wire.For(wirePath)!;
                var key = CacheKey.Of(wire, doc.RootElement, Cache, scope);
                var again = CacheKey.Of(wire, doc.RootElement, Cache, scope);
                FuzzInput.Check(
                    key is null ? again is null : again is not null && key.SequenceEqual(again),
                    "the cache key is not deterministic"
                );
            }
        }
    }

    private static RouteRule Rule() =>
        new()
        {
            From = ["*"],
            To = "cheap-1",
            Provider = "anthropic",
            Gate = "bench-1",
        };
}
