using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Jsonc;
using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// Claude's layout, which Qwen, CodeBuddy, Codex, Devin, ZCode and (without matchers) Gemini
/// share: <c>&lt;at&gt;.&lt;event&gt;[] = {matcher?, hooks: [{type: "command", command, &lt;timeout&gt;}]}</c>
/// (rtok <c>claude::insert_ours_as</c>, <c>strip_ours_as</c>).
/// </summary>
internal sealed class NestedHooks(string path) : OwnedFile(path)
{
    /// <summary>Where the event map sits: <c>hooks</c>, or ZCode's <c>hooks.events</c>.</summary>
    public required JsoncSegment[] At { get; init; }

    /// <summary>The (event, matcher) pairs rtok writes, in file order.</summary>
    public required IReadOnlyList<HookEventRow> Rows { get; init; }

    /// <summary>The command for an rtok event.</summary>
    public required Func<string, string> Command { get; init; }

    /// <summary>Whether a command is rtok's for an rtok event.</summary>
    public required Func<string, string, bool> Ours { get; init; }

    /// <summary>The timeout's key, <c>timeout</c> or ZCode's <c>timeoutMs</c>.</summary>
    public string TimeoutKey { get; init; } = "timeout";

    /// <summary>The timeout, in the key's unit.</summary>
    public required long Timeout { get; init; }

    /// <summary>
    /// Claude's rule that an rtok hook on a pair not listed is dropped, and that an event outside
    /// the table names its own rtok event. Gemini maps its events and prunes nothing.
    /// </summary>
    public bool Prune { get; init; } = true;

    /// <summary>Gemini's: an entry must hold only <c>hooks</c> to count as rtok's unchanged one.</summary>
    public bool Bare { get; init; }

    /// <summary>ZCode's <c>hooks.enabled = true</c>, set on every install.</summary>
    public bool Enable { get; init; }

    public override bool Holds(string text)
    {
        var doc = new JsonEdit(Path, text);
        return Hooks(doc).Any(h => h.Ours);
    }

    public override (string Report, string Text) Install(string text)
    {
        var doc = new JsonEdit(Path, text);
        var enabled = false;
        if (Enable && doc.Get("hooks", "enabled") is not { ValueKind: JsonValueKind.True })
        {
            doc.Object("hooks");
            doc.Set(["hooks", "enabled"], true);
            enabled = true;
        }
        var lines = new List<string>();
        if (Prune)
        {
            var gone = Hooks(doc).Where(h => h.Ours && !Listed(h.Event, h.Matcher)).ToList();
            lines.AddRange(gone.Select(h => $"- {h.Event}{HookText.Show(h.Matcher)} {h.Command}"));
            doc.RemoveHooks(At, gone.Select(h => (h.Event, h.Entry, h.Hook)));
        }
        foreach (var row in Rows)
        {
            Put(doc, row, lines);
        }
        var report = HookText.Counted(lines);
        if (enabled)
        {
            report =
                lines.Count == 0 ? "+ hooks.enabled = true" : "+ hooks.enabled = true\n" + report;
        }
        return (report, doc.Text);
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var doc = new JsonEdit(Path, text);
        var kept = new List<string>();
        var gone = Hooks(doc)
            .Where(h =>
                h.Ours
                && HookText.Takes(
                    apply,
                    Listed(h.Event, h.Matcher) && (!Bare || h.Bare) && Exact(h.Value, h.Command),
                    $"hooks.{h.Event}{HookText.Show(h.Matcher)} in {Path}",
                    kept
                )
            )
            .ToList();
        doc.RemoveHooks(At, gone.Select(h => (h.Event, h.Entry, h.Hook)));
        if (gone.Count > 0)
        {
            doc.DropEmpty(At);
        }
        return (HookText.Removed(kept, gone.Count), doc.Text);
    }

    /// <summary>One hook found in the file, where it sits and whether it is rtok's.</summary>
    private sealed record Found(
        string Event,
        int Entry,
        int Hook,
        string Matcher,
        bool Bare,
        string Command,
        JsonElement Value,
        bool Ours
    );

    /// <summary>Rewrites rtok's hooks on <paramref name="row"/>'s pair, or appends one.</summary>
    private void Put(JsonEdit doc, HookEventRow row, List<string> lines)
    {
        var want = Command(row.RtokEvent);
        var show = $"{row.HostEvent}{HookText.Show(row.Matcher)} {want}";
        var found = false;
        foreach (
            var h in Hooks(doc)
                .Where(h =>
                    h.Ours
                    && string.Equals(h.Event, row.HostEvent, StringComparison.Ordinal)
                    && (!Prune || string.Equals(h.Matcher, row.Matcher, StringComparison.Ordinal))
                )
                .ToList()
        )
        {
            found = true;
            if (
                string.Equals(h.Command, want, StringComparison.Ordinal)
                && JsonEdit.Is(h.Value, TimeoutKey, Timeout)
            )
            {
                continue;
            }
            JsoncSegment[] at = [.. At, h.Event, h.Entry, "hooks", h.Hook];
            doc.Set([.. at, "command"], want);
            doc.Set([.. at, TimeoutKey], Timeout);
            lines.Add("~ " + show);
        }
        if (found)
        {
            return;
        }
        var entry = new JsonObject();
        if (row.Matcher.Length > 0)
        {
            entry["matcher"] = row.Matcher;
        }
        entry["hooks"] = new JsonArray(Hook(want));
        doc.Object(At);
        doc.Append([.. At, row.HostEvent], entry);
        lines.Add("+ " + show);
    }

    private JsonObject Hook(string command) =>
        new()
        {
            ["type"] = "command",
            ["command"] = command,
            [TimeoutKey] = Timeout,
        };

    /// <summary>Exactly the hook rtok writes for <paramref name="command"/>.</summary>
    private bool Exact(JsonElement hook, string command) =>
        JsonEdit.Count(hook) == 3
        && JsonEdit.Str(hook, "type") is "command"
        && string.Equals(JsonEdit.Str(hook, "command"), command, StringComparison.Ordinal)
        && JsonEdit.Is(hook, TimeoutKey, Timeout);

    private bool Listed(string hostEvent, string matcher) =>
        Rows.Any(r =>
            string.Equals(r.HostEvent, hostEvent, StringComparison.Ordinal)
            && string.Equals(r.Matcher, matcher, StringComparison.Ordinal)
        );

    /// <summary>The rtok event a host event runs; under <see cref="Prune"/> an unlisted one names itself.</summary>
    private string? RtokEvent(string hostEvent) =>
        Rows.FirstOrDefault(r =>
            string.Equals(r.HostEvent, hostEvent, StringComparison.Ordinal)
        )?.RtokEvent
        ?? (Prune ? hostEvent : null);

    /// <summary>Every hook with a string command under <see cref="At"/>, in file order.</summary>
    private IEnumerable<Found> Hooks(JsonEdit doc)
    {
        if (doc.Object(At) is not { } events)
        {
            yield break;
        }
        foreach (var ev in events.EnumerateObject())
        {
            var rtok = RtokEvent(ev.Name);
            var entry = 0;
            foreach (var e in JsonEdit.Items(ev.Value))
            {
                var matcher = JsonEdit.Str(e, "matcher") ?? "";
                var bare = JsonEdit.Count(e) == 1;
                var hook = 0;
                foreach (
                    var h in e.ValueKind == JsonValueKind.Object
                    && e.TryGetProperty("hooks", out var list)
                        ? JsonEdit.Items(list)
                        : []
                )
                {
                    if (JsonEdit.Str(h, "command") is { } command)
                    {
                        var ours = rtok is not null && Ours(command, rtok);
                        yield return new Found(
                            ev.Name,
                            entry,
                            hook,
                            matcher,
                            bare,
                            command,
                            h,
                            ours
                        );
                    }
                    hook++;
                }
                entry++;
            }
        }
    }
}
