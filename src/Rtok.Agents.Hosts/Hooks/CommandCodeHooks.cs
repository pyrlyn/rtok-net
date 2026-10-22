using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// Command Code's <c>settings.json</c>: <c>hooks.&lt;event&gt;[] = {matcher?, hooks: [{type,
/// command, timeout}]}</c>, one definition per matcher, since Command Code matches the tool's
/// display name (rtok <c>commandcode::insert_ours</c>). A stale rtok hook is rewritten in place,
/// where rtok would add a second one beside it.
/// </summary>
internal sealed class CommandCodeHooks(string path, HostContext ctx) : HookFile(path)
{
    /// <summary>Command Code's own default; <c>setup.hook_timeout_s</c> does not reach it (rtok).</summary>
    private const long Timeout = 5;

    private static IReadOnlyList<HookEventRow> Rows => HookEvents.InstallerRows("commandcode");

    public override bool Holds(string text)
    {
        var doc = new HookJson(Path, text);
        return Rows.Any(r => Defs(doc, r.HostEvent).Exists(d => Hooks(d).Any(Ours)));
    }

    public override (string Report, string Text) Install(string text)
    {
        var doc = new HookJson(Path, text);
        doc.Object("hooks");
        var lines = new List<string>();
        foreach (var row in Rows)
        {
            var cmd = $"{HookText.Quote(ctx.Command)} hook {row.RtokEvent} --host commandcode";
            var hook = new JsonObject
            {
                ["type"] = "command",
                ["command"] = cmd,
                ["timeout"] = Timeout,
            };
            var defs = Defs(doc, row.HostEvent);
            var def = defs.FindIndex(d =>
                row.Matcher.Length > 0
                    ? string.Equals(
                        HookJson.Str(d, "matcher"),
                        row.Matcher,
                        StringComparison.Ordinal
                    )
                    : d.ValueKind == JsonValueKind.Object && !d.TryGetProperty("matcher", out _)
            );
            if (def < 0)
            {
                var entry = new JsonObject();
                if (row.Matcher.Length > 0)
                {
                    entry["matcher"] = row.Matcher;
                }
                entry["hooks"] = new JsonArray(hook);
                doc.Append(["hooks", row.HostEvent], entry);
                lines.Add($"+ {row.HostEvent} {cmd}");
                continue;
            }
            var hooks = Hooks(defs[def]).ToList();
            if (
                hooks.Exists(h =>
                    string.Equals(HookJson.Str(h, "command"), cmd, StringComparison.Ordinal)
                )
            )
            {
                continue;
            }
            if (hooks.FindIndex(Ours) is var stale and >= 0)
            {
                doc.Set(["hooks", row.HostEvent, def, "hooks", stale, "command"], cmd);
                lines.Add($"~ {row.HostEvent} {cmd}");
                continue;
            }
            doc.Append(["hooks", row.HostEvent, def, "hooks"], hook);
            lines.Add($"+ {row.HostEvent} {cmd}");
        }
        return (lines.Count == 0 ? Agent.Sdk.Apply.NoChanges : string.Join('\n', lines), doc.Text);
    }

    /// <summary>In each event, every rtok hook of the first definition holding one goes (rtok <c>strip_ours</c>).</summary>
    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var doc = new HookJson(Path, text);
        var lines = new List<string>();
        var gone = new List<(string, int, int)>();
        foreach (var ev in Rows.Select(r => r.HostEvent).Distinct(StringComparer.Ordinal))
        {
            var defs = Defs(doc, ev);
            for (var d = 0; d < defs.Count; d++)
            {
                var ours = Hooks(defs[d]).Select((h, i) => (h, i)).Where(x => Ours(x.h)).ToList();
                if (ours.Count > 0)
                {
                    gone.AddRange(ours.Select(x => (ev, d, x.i)));
                    lines.Add($"- {ev}");
                    break;
                }
            }
        }
        doc.RemoveHooks(["hooks"], gone);
        if (gone.Count > 0)
        {
            doc.DropEmpty("hooks");
        }
        return (lines.Count == 0 ? Agent.Sdk.Apply.NoChanges : string.Join('\n', lines), doc.Text);
    }

    private static List<JsonElement> Defs(HookJson doc, string hostEvent) =>
        HookJson.Items(doc.Array("hooks", hostEvent)).ToList();

    private static IEnumerable<JsonElement> Hooks(JsonElement def) =>
        def.ValueKind == JsonValueKind.Object && def.TryGetProperty("hooks", out var list)
            ? HookJson.Items(list)
            : [];

    /// <summary>A hook whose command is exactly an rtok bin running an event rtok writes here.</summary>
    private bool Ours(JsonElement hook) =>
        HookJson.Str(hook, "command") is { } c
        && Rows.Any(r =>
            HookText.Runs(ctx, c, $"hook {r.RtokEvent} --host commandcode", resolver: false)
        );
}
