using System.Text.Json.Nodes;

namespace Rtok.Agent.Sdk;

/// <summary>
/// The check every removal runs on an entry named <c>rtok</c>: rtok takes back only what it
/// wrote. An entry equal to what the installer writes goes; one the user edited asks first; a
/// foreign one is left.
/// </summary>
public static class Ownership
{
    /// <summary>
    /// <see langword="null"/> when <paramref name="have"/> is rtok's unchanged entry: remove it.
    /// Otherwise the report that keeps it: <c>leave … (not rtok's; remove by hand)</c> when no
    /// string in it names the rtok binary, or <see cref="KeepEdited"/> when it runs rtok but
    /// differs from <paramref name="ours"/> once every rtok binary string in both is folded to one
    /// placeholder and a top-level <c>"type": "stdio"</c> is dropped.
    /// </summary>
    /// <param name="apply">The run.</param>
    /// <param name="at">How the entry reads in a report, e.g. <c>mcpServers.rtok in /path</c>.</param>
    /// <param name="have">The entry found on disk.</param>
    /// <param name="ours">The entry the installer writes now.</param>
    /// <param name="isBin">Whether a string names the rtok binary, so any rtok path counts as the same.</param>
    public static string? Judge(
        Apply apply,
        string at,
        JsonNode? have,
        JsonNode? ours,
        Func<string, bool> isBin
    )
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(isBin);
        if (!RunsBin(have, isBin))
        {
            return $"leave {at} (not rtok's; remove by hand)";
        }
        var same = JsonNode.DeepEquals(
            RtokAsOne(WithoutDefaultType(have), isBin),
            RtokAsOne(WithoutDefaultType(ours), isBin)
        );
        return same ? null : KeepEdited(apply, at);
    }

    /// <summary>
    /// For an rtok entry the user changed: <see langword="null"/> removes it (<c>--yes</c>, or a
    /// yes to <c>remove … you changed it [y/N]</c>); otherwise the report that keeps it,
    /// <c>? …</c> on a dry run or <c>leave …</c> once declined.
    /// </summary>
    public static string? KeepEdited(Apply apply, string at)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (apply.DryRun && !apply.Yes)
        {
            return $"? {at} (changed by you; remove asks)";
        }
        return apply.Confirmed($"remove {at}? you changed it")
            ? null
            : $"leave {at} (changed by you; remove by hand)";
    }

    /// <summary>True when <paramref name="node"/>, or anything nested in it, is a string naming the rtok binary.</summary>
    public static bool RunsBin(JsonNode? node, Func<string, bool> isBin)
    {
        ArgumentNullException.ThrowIfNull(isBin);
        return node switch
        {
            JsonArray a => a.Any(x => RunsBin(x, isBin)),
            JsonObject o => o.Any(kv => RunsBin(kv.Value, isBin)),
            JsonValue v => v.TryGetValue<string>(out var s) && isBin(s),
            _ => false,
        };
    }

    /// <summary>
    /// <paramref name="node"/> without a top-level <c>"type": "stdio"</c>, the MCP default rtok
    /// spells out and Claude.app drops when it re-saves its config. Anything else is unchanged.
    /// </summary>
    public static JsonNode? WithoutDefaultType(JsonNode? node)
    {
        if (
            node is not JsonObject o
            || o["type"] is not JsonValue t
            || !t.TryGetValue<string>(out var type)
            || !string.Equals(type, "stdio", StringComparison.Ordinal)
        )
        {
            return node?.DeepClone();
        }
        var copy = (JsonObject)o.DeepClone();
        copy.Remove("type");
        return copy;
    }

    /// <summary>
    /// <paramref name="node"/> with every rtok binary string replaced by one placeholder, and the
    /// <c>--host &lt;id&gt;</c> pair after <c>mcp</c> dropped from arrays: entries written with and
    /// without it are both rtok's own.
    /// </summary>
    private static JsonNode? RtokAsOne(JsonNode? node, Func<string, bool> isBin) =>
        node switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) && isBin(s) => null,
            JsonArray a => new JsonArray([.. WithoutHostArg(a).Select(x => RtokAsOne(x, isBin))]),
            JsonObject o => new JsonObject(
                o.Select(kv => KeyValuePair.Create(kv.Key, RtokAsOne(kv.Value, isBin)))
            ),
            _ => node?.DeepClone(),
        };

    private static IEnumerable<JsonNode?> WithoutHostArg(JsonArray argv)
    {
        var afterMcp = false;
        var skip = false;
        foreach (var x in argv)
        {
            if (skip)
            {
                skip = false;
                continue;
            }
            var s = x is JsonValue v && v.TryGetValue<string>(out var str) ? str : null;
            if (afterMcp && string.Equals(s, "--host", StringComparison.Ordinal))
            {
                skip = true;
                continue;
            }
            afterMcp = string.Equals(s, "mcp", StringComparison.Ordinal);
            yield return x;
        }
    }
}
