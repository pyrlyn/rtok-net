using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// Copilot's <c>hooks/rtok.json</c>, a file rtok owns whole: <c>{version: 1, hooks: {&lt;event&gt;:
/// [{type, bash, powershell, timeoutSec}]}}</c> (rtok <c>copilot::write_hooks</c>). Install writes
/// it when it differs, remove deletes it.
/// </summary>
internal sealed class CopilotHooks(string path, HostContext ctx) : HookFile(path)
{
    private const string Note =
        "{\"additionalContext\":\"rtok is not installed; run ketch install pyrlyn/rtok to enable it.\"}";

    private static IReadOnlyList<HookEventRow> Rows => HookEvents.InstallerRows("copilot");

    public override bool Holds(string text) =>
        text.Contains(" hook PreToolUse --host copilot", StringComparison.Ordinal);

    public override (string Report, string Text) Install(string text)
    {
        var want = HostFile.ToJson(Document());
        if (text.Length > 0 && Same(text, want))
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        return ($"+ {Path} ({Rows.Count} events)", want);
    }

    public override (string Report, string Text) Remove(string text, Apply apply) =>
        (Agent.Sdk.Apply.NoChanges, text);

    public override string Apply(bool remove, Apply apply, McpDisk disk)
    {
        if (!remove)
        {
            return base.Apply(remove, apply, disk);
        }
        if (disk.Read(Path) is null)
        {
            return Agent.Sdk.Apply.NoChanges;
        }
        var report = $"- {Path}";
        disk.Delete(apply, Path, report);
        return report;
    }

    /// <summary>The file as rtok writes it.</summary>
    private JsonObject Document()
    {
        var hooks = new JsonObject();
        var bin = HookText.Quote(ctx.Command);
        foreach (var row in Rows)
        {
            var args = $"hook {row.RtokEvent} --host copilot";
            var note = row.RtokEvent is "SessionStart" ? Note : null;
            var plain = bin is not "rtok";
            hooks[row.HostEvent] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "command",
                    ["bash"] = plain ? $"{bin} {args}" : HookLines.Posix(args, note),
                    ["powershell"] = plain ? $"{bin} {args}" : HookLines.PowerShell(args, note),
                    ["timeoutSec"] = ctx.Settings.HookTimeoutS,
                }
            );
        }
        return new JsonObject { ["version"] = 1, ["hooks"] = hooks };
    }

    /// <summary>Whether <paramref name="text"/> already holds <paramref name="want"/>, whatever its layout.</summary>
    private static bool Same(string text, string want)
    {
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(text), JsonNode.Parse(want));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
