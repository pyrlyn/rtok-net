using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;
using Tomlyn.Model;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// Kimi's <c>config.toml</c>: one <c>[[hooks]]</c> table per (event, matcher) pair, <c>event</c>,
/// <c>matcher</c> when set, <c>command = "&lt;bin&gt; hook &lt;event&gt;"</c> and <c>timeout</c>
/// (rtok <c>kimi::insert_ours</c>). A stale rtok table is rewritten in place, one on a pair no
/// longer listed goes; foreign tables are never touched.
/// </summary>
internal sealed class KimiHooks(string path, HostContext ctx) : HookFile(path)
{
    private static IReadOnlyList<HookEventRow> Rows => HookEvents.ForHost("kimi");

    private long Timeout => ctx.Settings.HookTimeoutS;

    public override bool Holds(string text) => Tables(new TomlHooks(Path, text)).Exists(Ours);

    public override (string Report, string Text) Install(string text)
    {
        var toml = new TomlHooks(Path, text);
        var tables = Tables(toml);
        var lines = new List<string>();
        var kept = new List<TomlHooks.Table>();
        foreach (var t in tables)
        {
            if (Ours(t) && !Listed(t))
            {
                toml.Drop(t);
                lines.Add($"- [[hooks]] {Event(t)}{HookText.Show(Matcher(t))}");
            }
            else
            {
                kept.Add(t);
            }
        }
        var bin = HookText.Quote(ctx.Command);
        foreach (var row in Rows)
        {
            var cmd = $"{bin} hook {row.HostEvent}";
            var show = $"[[hooks]] {row.HostEvent}{HookText.Show(row.Matcher)} {cmd}";
            var mine = kept.Where(t => Ours(t) && Is(t, row)).ToList();
            foreach (
                var t in mine.Where(t =>
                    !string.Equals(t.Str("command"), cmd, StringComparison.Ordinal)
                    || t.Int("timeout") != Timeout
                )
            )
            {
                toml.Set(t, "command", cmd);
                toml.Set(t, "timeout", Timeout);
                lines.Add("~ " + show);
            }
            if (mine.Count == 0)
            {
                toml.Append(
                    "[[hooks]]",
                    [
                        ("event", row.HostEvent),
                        .. row.Matcher.Length > 0
                            ? [("matcher", (object)row.Matcher)]
                            : Array.Empty<(string, object)>(),
                        ("command", cmd),
                        ("timeout", Timeout),
                    ]
                );
                lines.Add("+ " + show);
            }
        }
        return (HookText.Counted(lines), toml.Edited());
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var toml = new TomlHooks(Path, text);
        var kept = new List<string>();
        var removed = 0;
        foreach (var t in Tables(toml).Where(Ours))
        {
            var keys = t.Keys.ContainsKey("matcher") ? 4 : 3;
            var unchanged = Listed(t) && t.Keys.Count == keys && t.Int("timeout") == Timeout;
            var at = $"[[hooks]] {Event(t)}{HookText.Show(Matcher(t))} in {Path}";
            if (HookText.Takes(apply, unchanged, at, kept))
            {
                toml.Drop(t);
                removed++;
            }
        }
        return (HookText.Removed(kept, removed), toml.Edited());
    }

    /// <summary>The <c>[[hooks]]</c> tables; hooks spelled any other way are refused.</summary>
    private static List<TomlHooks.Table> Tables(TomlHooks toml)
    {
        if (toml.Model.TryGetValue("hooks", out var hooks) && hooks is not TomlTableArray)
        {
            throw toml.Shape("`hooks` is not an array of tables");
        }
        return toml.Tables("hooks");
    }

    private static string Event(TomlHooks.Table t) => t.Str("event") ?? "";

    private static string Matcher(TomlHooks.Table t) => t.Str("matcher") ?? "";

    private static bool Is(TomlHooks.Table t, HookEventRow row) =>
        string.Equals(Event(t), row.HostEvent, StringComparison.Ordinal)
        && string.Equals(Matcher(t), row.Matcher, StringComparison.Ordinal);

    private static bool Listed(TomlHooks.Table t) => Rows.Any(r => Is(t, r));

    /// <summary>A table whose command is rtok's for its own event.</summary>
    private bool Ours(TomlHooks.Table t) =>
        t.Str("command") is { } c && HookText.Runs(ctx, c, $"hook {Event(t)}");
}
