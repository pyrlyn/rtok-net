using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// Cursor's <c>hooks.json</c>: <c>{version: 1, hooks: {&lt;event&gt;: [{command}]}}</c> (rtok
/// <c>cursor::insert_ours</c>). Off Windows the resolver is braced, <c>{ …; }</c>, since Cursor
/// appends the payload heredoc to the last command of the line.
/// </summary>
internal sealed class CursorHooks(string path, HostContext ctx) : OwnedFile(path)
{
    // An earlier build wrote UserPromptSubmit here; its reply has no context field (rtok T390.1).
    private static readonly HookEventRow Retired = new(
        "cursor",
        "beforeSubmitPrompt",
        "UserPromptSubmit",
        "",
        false
    );

    private static IReadOnlyList<HookEventRow> Rows => HookEvents.InstallerRows("cursor");

    /// <summary>What the installer writes plus what it still takes back.</summary>
    private static IEnumerable<HookEventRow> Owned => Rows.Append(Retired);

    public override bool Holds(string text)
    {
        var doc = new JsonEdit(Path, text);
        return Owned.Any(r => JsonEdit.Items(doc.Array("hooks", r.HostEvent)).Any(Ours));
    }

    public override (string Report, string Text) Install(string text)
    {
        var doc = new JsonEdit(Path, text);
        doc.Object("hooks");
        var lines = new List<string>();
        var retired = JsonEdit.Items(doc.Array("hooks", Retired.HostEvent)).ToList();
        if (retired.Exists(Ours))
        {
            Drop(doc, Retired.HostEvent, retired, _ => true);
            lines.Add($"- {Retired.HostEvent}");
        }
        foreach (var row in Rows)
        {
            var cmd = Command(row.RtokEvent);
            var found = false;
            var i = 0;
            foreach (var e in JsonEdit.Items(doc.Array("hooks", row.HostEvent)).ToList())
            {
                if (Ours(e))
                {
                    found = true;
                    if (!string.Equals(JsonEdit.Str(e, "command"), cmd, StringComparison.Ordinal))
                    {
                        doc.Set(["hooks", row.HostEvent, i, "command"], cmd);
                        lines.Add($"~ {row.HostEvent} {cmd}");
                    }
                }
                i++;
            }
            if (!found)
            {
                doc.Append(["hooks", row.HostEvent], new JsonObject { ["command"] = cmd });
                lines.Add($"+ {row.HostEvent} {cmd}");
            }
        }
        if (doc.Get("version") is null)
        {
            doc.Set(["version"], 1);
        }
        return (lines.Count == 0 ? Agent.Sdk.Apply.NoChanges : string.Join('\n', lines), doc.Text);
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var doc = new JsonEdit(Path, text);
        var (lines, kept) = (new List<string>(), new List<string>());
        foreach (var row in Owned)
        {
            var entries = JsonEdit.Items(doc.Array("hooks", row.HostEvent)).ToList();
            var suffix = $" hook {row.RtokEvent} --host cursor";
            if (
                Drop(
                    doc,
                    row.HostEvent,
                    entries,
                    e =>
                        HookText.Takes(
                            apply,
                            JsonEdit.Count(e) == 1
                                && JsonEdit.Str(e, "command") is { } c
                                && (
                                    c.EndsWith(suffix, StringComparison.Ordinal)
                                    || string.Equals(
                                        c,
                                        Braced(row.RtokEvent),
                                        StringComparison.Ordinal
                                    )
                                ),
                            $"hooks.{row.HostEvent} in {Path}",
                            kept
                        )
                )
            )
            {
                lines.Add($"- {row.HostEvent}");
            }
        }
        if (lines.Count > 0)
        {
            doc.DropEmpty("hooks");
        }
        kept.AddRange(lines);
        return (kept.Count == 0 ? Agent.Sdk.Apply.NoChanges : string.Join('\n', kept), doc.Text);
    }

    /// <summary>Removes rtok's entries of <paramref name="hostEvent"/> that <paramref name="take"/> lets go; true when any went.</summary>
    private bool Drop(
        JsonEdit doc,
        string hostEvent,
        List<System.Text.Json.JsonElement> entries,
        Func<System.Text.Json.JsonElement, bool> take
    )
    {
        var gone = entries
            .Select((e, i) => (e, i))
            .Where(x => Ours(x.e) && take(x.e))
            .Select(x => x.i)
            .ToList();
        foreach (var i in gone.OrderDescending())
        {
            doc.Remove("hooks", hostEvent, i);
        }
        if (gone.Count > 0 && doc.Array("hooks", hostEvent)?.GetArrayLength() == 0)
        {
            doc.Remove("hooks", hostEvent);
        }
        return gone.Count > 0;
    }

    private string Command(string rtokEvent)
    {
        var bin = HookText.Quote(ctx.Command);
        return ctx.Env.Os == McpOs.Windows || bin is not "rtok"
            ? $"{bin} hook {rtokEvent} --host cursor"
            : Braced(rtokEvent);
    }

    private static string Braced(string rtokEvent) =>
        $"{{ {HookLines.Posix($"hook {rtokEvent} --host cursor")}; }}";

    /// <summary>An entry whose command runs rtok for any event rtok owns here.</summary>
    private bool Ours(System.Text.Json.JsonElement entry) =>
        JsonEdit.Str(entry, "command") is { } c
        && Owned.Any(r => HookText.Runs(ctx, c, $"hook {r.RtokEvent} --host cursor"));
}
