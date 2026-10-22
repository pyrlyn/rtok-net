using System.Text.Json;
using Rtok.Agents.Usage;

namespace Rtok.Measure;

/// <summary>
/// The <c>subagents</c> row: <c>&lt;session&gt;/subagents/agent-&lt;id&gt;.jsonl</c> with its
/// <c>agent-&lt;id&gt;.meta.json</c> (<c>agentType</c>, <c>model</c>, <c>toolUseId</c>), counted
/// once toward the parent session. A re-read is a sub-agent <c>Read</c> of a path the parent read
/// or an earlier sibling read; parent first, so the two re-read columns are disjoint.
/// </summary>
internal sealed class Subagents
{
    /// <summary>
    /// The fixed words of a <c>SubagentStart</c> spawn brief: a sub-agent whose first user
    /// message holds them was spawned with one.
    /// </summary>
    public const string SpawnBriefMarker = "Get the archived body with rtok expand";

    /// <summary>Parent sessions with at least one sub-agent.</summary>
    public long Sessions { get; set; }
    public long Count { get; set; }

    /// <summary>Tool-result bytes of the sub-agents.</summary>
    public long ResultBytes { get; set; }

    /// <summary>Tool-result bytes of their parents' whole transcripts.</summary>
    public long ParentResultBytes { get; set; }

    /// <summary>Native <c>Read</c> result bytes in the sub-agents.</summary>
    public long ReadBytes { get; set; }

    /// <summary>… of a path the parent also read (any time in its session).</summary>
    public long ReadParent { get; set; }

    /// <summary>… of a path an earlier sibling read and the parent did not.</summary>
    public long ReadSibling { get; set; }
    public long RereadBytes { get; set; }

    /// <summary>How many re-reads there were: the <c>subagent</c> class of repeat reads.</summary>
    public long RereadCalls { get; set; }

    /// <summary>Re-read bytes over sub-agent read bytes, %.</summary>
    public double ShareRead { get; set; }

    /// <summary>Re-read bytes over sub-agent tool-result bytes, %.</summary>
    public double ShareResults { get; set; }

    /// <summary>Re-read bytes over the tree's (sub-agents and parents) tool-result bytes, %.</summary>
    public double ShareTree { get; set; }
    public long UsageInput { get; set; }
    public long UsageCacheRead { get; set; }
    public long UsageCacheWrite { get; set; }
    public long UsageOutput { get; set; }

    /// <summary>Split by <c>agentType</c> and <c>model</c> (<c>unknown</c> and <c>-</c> when absent).</summary>
    public IReadOnlyList<TypeRow> ByType { get; set; } = [];
    public BriefSplit WithBrief { get; } = new();
    public BriefSplit WithoutBrief { get; } = new();

    /// <summary>One <c>(agentType, model)</c> row.</summary>
    internal sealed class TypeRow
    {
        public string AgentType { get; set; } = "";
        public string Model { get; set; } = "";
        public long Count { get; set; }
        public long ResultBytes { get; set; }
        public long ReadBytes { get; set; }
        public long RereadBytes { get; set; }
    }

    /// <summary>Sub-agents spawned with, or without, a spawn brief.</summary>
    internal sealed class BriefSplit
    {
        public long Count { get; set; }
        public long ReadBytes { get; set; }
        public long RereadBytes { get; set; }
        public double ShareRead { get; set; }
        public long UsageInput { get; set; }
    }

    /// <summary>
    /// What the walk needs from one parent transcript: its sidecar folder (the transcript path
    /// without its extension), every path a native <c>Read</c> named, the spawn order of its
    /// <c>Agent</c>/<c>Task</c> calls, and its tool-result bytes.
    /// </summary>
    internal sealed class Parent
    {
        public Parent(string transcript, Transcript parsed)
        {
            Sidecar = Path.ChangeExtension(transcript, null);
            foreach (var u in parsed.ToolUses)
            {
                switch (u.Name)
                {
                    case "Read" when Calls.ToolPath(u.Input) is { } p:
                        ReadPaths.Add(p);
                        break;
                    case "Agent" or "Task":
                        SpawnOrder.TryAdd(u.Id, SpawnOrder.Count);
                        break;
                }
            }
            ResultBytes = parsed.ToolResults.Sum(r => Transcript.Utf8(r.Content));
        }

        public string Sidecar { get; }
        public List<string> ReadPaths { get; } = [];
        public Dictionary<string, int> SpawnOrder { get; } = new(StringComparer.Ordinal);
        public long ResultBytes { get; }
    }

    /// <summary>True for a transcript in a <c>subagents/</c> folder: never a session of its own.</summary>
    public static bool IsSubagent(string path) =>
        Path.GetFileName(Path.GetDirectoryName(path)) is "subagents";

    /// <summary>
    /// Every <c>&lt;stem&gt;/subagents/*.jsonl</c> modified at or after <paramref name="cutoff"/>,
    /// attributed to its parent; null when no parent had one.
    /// </summary>
    public static Subagents? Collect(IReadOnlyList<Parent> parents, DateTime cutoff)
    {
        var out_ = new Subagents();
        var byType = new SortedDictionary<string, TypeRow>(StringComparer.Ordinal);
        foreach (var parent in parents)
        {
            var agents = Spawned(parent, cutoff);
            if (agents.Count == 0)
            {
                continue;
            }
            out_.Sessions++;
            out_.ParentResultBytes += parent.ResultBytes;
            var siblingPaths = new List<string>();
            foreach (var (_, _, path, agentType, model) in agents)
            {
                if (Transcript.Read(path) is not { } parsed)
                {
                    continue;
                }
                out_.Count++;
                out_.Agent(parent, parsed, siblingPaths, Row(byType, agentType, model));
            }
        }
        if (out_.Count == 0)
        {
            return null;
        }
        out_.ShareRead = Calls.Pct(out_.RereadBytes, out_.ReadBytes);
        out_.ShareResults = Calls.Pct(out_.RereadBytes, out_.ResultBytes);
        out_.ShareTree = Calls.Pct(out_.RereadBytes, out_.ResultBytes + out_.ParentResultBytes);
        foreach (var split in (BriefSplit[])[out_.WithBrief, out_.WithoutBrief])
        {
            split.ShareRead = Calls.Pct(split.RereadBytes, split.ReadBytes);
        }
        out_.ByType = [.. byType.Values];
        return out_;
    }

    private void Agent(Parent parent, Transcript parsed, List<string> siblingPaths, TypeRow row)
    {
        var withBrief =
            parsed.FirstUserText?.Contains(SpawnBriefMarker, StringComparison.Ordinal) is true;
        var (readBytes, rereadBytes, input) = (0L, 0L, 0L);
        var (names, paths, readPaths) = Index(parsed);
        row.Count++;
        foreach (var r in parsed.ToolResults)
        {
            var bytes = Transcript.Utf8(r.Content);
            ResultBytes += bytes;
            row.ResultBytes += bytes;
            if (!names.TryGetValue(r.ToolUseId, out var name) || name is not "Read")
            {
                continue;
            }
            ReadBytes += bytes;
            row.ReadBytes += bytes;
            readBytes += bytes;
            if (!paths.TryGetValue(r.ToolUseId, out var path))
            {
                continue;
            }
            // Parent first: the two re-read columns stay disjoint and sum to the total.
            if (parent.ReadPaths.Exists(p => Calls.SamePath(p, path)))
            {
                ReadParent += bytes;
            }
            else if (siblingPaths.Exists(p => Calls.SamePath(p, path)))
            {
                ReadSibling += bytes;
            }
            else
            {
                continue;
            }
            RereadBytes += bytes;
            RereadCalls++;
            row.RereadBytes += bytes;
            rereadBytes += bytes;
        }
        foreach (var u in parsed.Usages)
        {
            UsageInput += u.Input;
            UsageCacheRead += u.CacheRead;
            UsageCacheWrite += u.CacheCreate;
            UsageOutput += u.Output;
            input += u.Input;
        }
        var split = withBrief ? WithBrief : WithoutBrief;
        split.Count++;
        split.ReadBytes += readBytes;
        split.RereadBytes += rereadBytes;
        split.UsageInput += input;
        siblingPaths.AddRange(readPaths);
    }

    /// <summary>A transcript's calls by id: every tool name, each <c>Read</c>'s path, and those paths in call order.</summary>
    private static (
        Dictionary<string, string> Names,
        Dictionary<string, string> Paths,
        List<string> ReadPaths
    ) Index(Transcript parsed)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var readPaths = new List<string>();
        foreach (var u in parsed.ToolUses)
        {
            names[u.Id] = u.Name;
            if (u.Name is "Read" && Calls.ToolPath(u.Input) is { } p)
            {
                paths[u.Id] = p;
                readPaths.Add(p);
            }
        }
        return (names, paths, readPaths);
    }

    private static TypeRow Row(
        SortedDictionary<string, TypeRow> byType,
        string agentType,
        string model
    )
    {
        // NUL sorts first, so the key orders as the (type, model) pair does.
        var key = agentType + "\0" + model;
        if (!byType.TryGetValue(key, out var row))
        {
            row = new TypeRow { AgentType = agentType, Model = model };
            byType[key] = row;
        }
        return row;
    }

    /// <summary>
    /// The parent's sub-agent transcripts in spawn order: the parent's <c>Agent</c>/<c>Task</c>
    /// call order through the meta's <c>toolUseId</c>, unknown metas last by name.
    /// </summary>
    private static List<(int Order, string Stem, string Path, string Type, string Model)> Spawned(
        Parent parent,
        DateTime cutoff
    )
    {
        var agents = LogFiles
            .Find(Path.Combine(parent.Sidecar, "subagents"), cutoff, "jsonl")
            .Select(p =>
            {
                var (type, model, order) = Meta(p, parent.SpawnOrder);
                return (order, Path.GetFileNameWithoutExtension(p), p, type, model);
            })
            .ToList();
        agents.Sort(
            (a, b) =>
                a.order != b.order ? a.order.CompareTo(b.order)
                : string.CompareOrdinal(a.Item2, b.Item2) is var s and not 0 ? s
                : string.CompareOrdinal(a.p, b.p) is var q and not 0 ? q
                : string.CompareOrdinal(a.type, b.type) is var t and not 0 ? t
                : string.CompareOrdinal(a.model, b.model)
        );
        return agents;
    }

    /// <summary><c>(agentType, model, spawn order)</c> from the sidecar meta; an unknown one sorts last.</summary>
    internal static (string Type, string Model, int Order) Meta(
        string jsonl,
        Dictionary<string, int> spawnOrder
    )
    {
        var path = Path.ChangeExtension(jsonl, "meta.json");
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ("unknown", "-", int.MaxValue);
        }
        if (Json.Parse(text) is not { ValueKind: JsonValueKind.Object } v)
        {
            return ("unknown", "-", int.MaxValue);
        }
        var order =
            Json.Text(v, "toolUseId") is { } id && spawnOrder.TryGetValue(id, out var o)
                ? o
                : int.MaxValue;
        return (Json.Text(v, "agentType") ?? "unknown", Json.Text(v, "model") ?? "-", order);
    }
}
