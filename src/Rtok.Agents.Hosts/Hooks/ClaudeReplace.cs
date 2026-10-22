using System.Text.Json;
using Pyrlyn.Jsonc;
using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Proxy;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// <c>rtok agents install claude --replace</c> (rtok <c>claude::migrate</c>): takes the token
/// hooks of the tools rtok replaces out of Claude's settings, and their MCP servers out of the
/// settings and <c>~/.claude.json</c>, and points an <c>ANTHROPIC_BASE_URL</c> at a legacy proxy
/// port on loopback at rtok's proxy. It installs nothing.
/// </summary>
internal static class ClaudeReplace
{
    private static readonly string[] Legacy =
    [
        "rtk hook",
        "lean-ctx hook",
        "caveman-proxy",
        "caveman shrink-hook",
        "python-launcher.sh",
    ];

    private static readonly string[] DropMcp = ["lean-ctx", "code-review-graph"];

    /// <summary>The loopback ports the proxies rtok replaces listen on.</summary>
    private static readonly int[] LegacyPorts = [8788, 8787];

    /// <summary>The migration's report: one <c>- &lt;cmd&gt;</c> per hook taken, the base URL retargeted, the MCP servers dropped, the rtok hooks left.</summary>
    /// <exception cref="AgentsException">Neither <c>--yes</c> nor <c>--dry-run</c>: the hooks taken are not rtok's.</exception>
    /// <exception cref="IOException">A file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">A file does not parse, or is not shaped as Claude writes it.</exception>
    public static string Run(HostContext ctx, Apply apply, string settings)
    {
        if (!apply.DryRun && !apply.Yes)
        {
            throw new AgentsException("--replace requires --yes (or --dry-run)");
        }
        var text = ctx.Disk.Read(settings) ?? "";
        var doc = new JsonEdit(settings, text);
        var lines = StripHooks(doc);
        if (Retarget(doc, ctx.Settings.ProxyUrl) is { } retarget)
        {
            lines.Add(retarget);
        }
        if (StripMcp(doc) is { } mcp)
        {
            lines.Add(mcp);
        }
        lines.Add($"remaining rtok hooks: {Count(doc)}");
        var claudeJson = ctx.Probe.Expand("~/.claude.json");
        if (
            !string.Equals(claudeJson, settings, StringComparison.Ordinal)
            && ctx.Disk.Read(claudeJson) is { } raw
        )
        {
            var other = new JsonEdit(claudeJson, raw);
            if (StripMcp(other) is { } line)
            {
                lines.Add(line);
                ctx.Disk.Write(apply, claudeJson, other.Text, line);
            }
        }
        var report = string.Join('\n', lines);
        if (!string.Equals(doc.Text, text, StringComparison.Ordinal))
        {
            ctx.Disk.Write(apply, settings, doc.Text, report);
        }
        return report;
    }

    /// <summary>Takes every legacy hook out; an entry or event left empty goes too.</summary>
    private static List<string> StripHooks(JsonEdit doc)
    {
        var lines = new List<string>();
        if (doc.Object("hooks") is not { } events)
        {
            return lines;
        }
        var nested = new List<(string, int, int)>();
        var flat = new List<(string Event, int Entry)>();
        foreach (var ev in events.EnumerateObject())
        {
            var entry = 0;
            foreach (var e in JsonEdit.Items(ev.Value))
            {
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("hooks", out var list))
                {
                    var hook = 0;
                    foreach (var h in JsonEdit.Items(list))
                    {
                        if (JsonEdit.Str(h, "command") is { } cmd && IsLegacy(cmd))
                        {
                            nested.Add((ev.Name, entry, hook));
                            lines.Add($"- {cmd}");
                        }
                        hook++;
                    }
                }
                else if (JsonEdit.Str(e, "command") is { } cmd && IsLegacy(cmd))
                {
                    flat.Add((ev.Name, entry));
                    lines.Add($"- {cmd}");
                }
                entry++;
            }
        }
        // Flat entries hold no hook list, so taking them first leaves the nested indices valid.
        foreach (var (ev, entry) in flat.OrderByDescending(f => f.Entry))
        {
            doc.Remove("hooks", ev, entry);
        }
        doc.RemoveHooks(["hooks"], Shift(nested, flat));
        foreach (var ev in flat.Select(f => f.Event).Distinct(StringComparer.Ordinal))
        {
            if (doc.Array("hooks", ev)?.GetArrayLength() == 0)
            {
                doc.Remove("hooks", ev);
            }
        }
        if (lines.Count > 0)
        {
            doc.DropEmpty("hooks");
        }
        return lines;
    }

    /// <summary>The nested indices once the flat entries before them are gone.</summary>
    private static IEnumerable<(string, int, int)> Shift(
        List<(string Event, int Entry, int Hook)> nested,
        List<(string Event, int Entry)> flat
    ) =>
        nested.Select(n =>
            (
                n.Event,
                n.Entry
                    - flat.Count(f =>
                        string.Equals(f.Event, n.Event, StringComparison.Ordinal)
                        && f.Entry < n.Entry
                    ),
                n.Hook
            )
        );

    /// <summary>
    /// <c>env.ANTHROPIC_BASE_URL</c> on a legacy port moved to <paramref name="proxy"/>, its path,
    /// query and fragment kept (rtok <c>retarget_proxy</c>); null when it is not one.
    /// </summary>
    private static string? Retarget(JsonEdit doc, string proxy)
    {
        if (
            doc.Get("env") is not { } env
            || JsonEdit.Str(env, "ANTHROPIC_BASE_URL") is not { } url
            || ProxyOwner.LoopbackPort(url) is not { } port
            || !LegacyPorts.Contains(port)
        )
        {
            return null;
        }
        var after = url[(url.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var tail = after.IndexOfAny(['/', '?', '#']) is var at and >= 0 ? after[at..] : "";
        var next = proxy + tail;
        doc.Set(["env", "ANTHROPIC_BASE_URL"], next);
        return $"ANTHROPIC_BASE_URL {url} -> {next}";
    }

    private static string? StripMcp(JsonEdit doc)
    {
        var dropped = DropMcp.Where(name => doc.Get("mcpServers", name) is not null).ToList();
        foreach (var name in dropped)
        {
            doc.Remove(new JsoncSegment[] { "mcpServers", name });
        }
        return dropped.Count == 0 ? null : $"disabled MCP: {string.Join(", ", dropped)}";
    }

    private static int Count(JsonEdit doc) =>
        doc.Object("hooks") is { } events
            ? events
                .EnumerateObject()
                .SelectMany(ev => JsonEdit.Items(ev.Value))
                .SelectMany(e =>
                    e.ValueKind == JsonValueKind.Object && e.TryGetProperty("hooks", out var list)
                        ? JsonEdit.Items(list)
                        : []
                )
                .Count(h =>
                    JsonEdit.Str(h, "command")?.Contains("rtok hook", StringComparison.Ordinal)
                    == true
                )
            : 0;

    private static bool IsLegacy(string cmd) =>
        Legacy.Any(p => cmd.Contains(p, StringComparison.Ordinal));
}
