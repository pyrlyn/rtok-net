using Rtok.Agent.Sdk;
using Rtok.Mcp.Install;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// CodeWhale's <c>config.toml</c>: <c>[hooks] enabled = true</c> and one <c>[[hooks.hooks]]</c>
/// table, <c>name = "rtok"</c>, <c>event = "message_submit"</c>, the command and
/// <c>timeout_secs</c> (rtok <c>codewhale::insert_ours</c>). <c>enabled</c> is set only when absent,
/// so a user's <c>enabled = false</c> stays, and stays after remove.
/// </summary>
internal sealed class CodeWhaleHooks(string path, HostContext ctx) : OwnedFile(path)
{
    private const string Event = "message_submit";
    private const string Args = "hook UserPromptSubmit --host codewhale";

    private long Timeout => ctx.Settings.HookTimeoutS;

    public override bool Holds(string text) => Tables(new TomlEdit(Path, text)).Exists(Ours);

    public override (string Report, string Text) Install(string text)
    {
        var toml = new TomlEdit(Path, text);
        if (Tables(toml).Exists(Ours))
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        var header = toml.Doc.Tables.FirstOrDefault(t =>
            t is TableSyntax && TomlServers.Parts(t.Name!) is ["hooks"]
        );
        var hooks = toml.Model.TryGetValue("hooks", out var h) ? h as TomlTable : null;
        if (hooks?.ContainsKey("enabled") != true)
        {
            if (header is not null)
            {
                var last =
                    header.Items.ChildrenCount > 0
                        ? (SyntaxNode)header.Items.GetChild(header.Items.ChildrenCount - 1)!
                        : header.Name!;
                toml.InsertAfter(last, "enabled = true");
            }
            else if (hooks is null || hooks.Count == 1 && hooks.ContainsKey("hooks"))
            {
                toml.Append("[hooks]", [("enabled", true)]);
            }
            else
            {
                throw toml.Shape("`hooks` is not spelled as a [hooks] table");
            }
        }
        var cmd = $"{HookText.Quote(ctx.Command)} {Args}";
        toml.Append(
            "[[hooks.hooks]]",
            [("name", "rtok"), ("event", Event), ("command", cmd), ("timeout_secs", Timeout)]
        );
        return ($"+ [[hooks.hooks]] {Event} {cmd}\n1 addition", toml.Edited());
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var toml = new TomlEdit(Path, text);
        var kept = new List<string>();
        var removed = 0;
        foreach (var t in Tables(toml).Where(Ours))
        {
            var unchanged =
                t.Keys.Count == 4 && t.Str("name") is "rtok" && t.Int("timeout_secs") == Timeout;
            if (HookText.Takes(apply, unchanged, $"[[hooks.hooks]] {Event} in {Path}", kept))
            {
                toml.Drop(t);
                removed++;
            }
        }
        return (HookText.Removed(kept, removed), toml.Edited());
    }

    /// <summary>The <c>[[hooks.hooks]]</c> tables; hooks spelled any other way are refused.</summary>
    private static List<TomlEdit.Table> Tables(TomlEdit toml)
    {
        if (toml.Model.TryGetValue("hooks", out var hooks))
        {
            if (
                hooks is not TomlTable t
                || t.TryGetValue("hooks", out var list) && list is not TomlTableArray
            )
            {
                throw toml.Shape("`hooks.hooks` is not an array of tables");
            }
        }
        return toml.Tables("hooks", "hooks");
    }

    private bool Ours(TomlEdit.Table t) =>
        t.Str("event") is Event
        && t.Str("command") is { } c
        && HookText.Runs(ctx, c, Args, resolver: false);
}
