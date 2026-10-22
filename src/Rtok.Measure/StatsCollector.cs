using Rtok.Agents.Usage;
using static Rtok.Measure.StatsReport;

namespace Rtok.Measure;

/// <summary>Folds every transcript under a folder into one <see cref="StatsReport"/>.</summary>
internal sealed class StatsCollector
{
    private readonly string _plugin;
    private readonly Replay _replay;
    private readonly StatsSignals _signals;
    private readonly StatsReport _report = new();
    private readonly List<long> _finals = [];
    private readonly SizeRows _skillSamples = new();

    private StatsCollector(string plugin, Replay replay, StatsSignals signals) =>
        (_plugin, _replay, _signals) = (plugin, replay, signals);

    /// <summary>
    /// Every <c>*.jsonl</c> under <paramref name="dir"/> modified at or after
    /// <paramref name="cutoff"/>, in path order (the totals are order-free, the bounded p95
    /// samples are not). <paramref name="plugin"/> keeps tool results whose tool name equals or
    /// contains it; empty keeps all.
    /// </summary>
    public static StatsReport Collect(
        string dir,
        DateTime cutoff,
        string plugin,
        Replay replay,
        StatsSignals signals
    )
    {
        var c = new StatsCollector(plugin, replay, signals);
        var report = c._report;
        var parents = new List<Subagents.Parent>();
        foreach (var path in LogFiles.Find(dir, cutoff, "jsonl"))
        {
            // A sub-agent transcript counts toward its parent below, never as a session.
            if (Subagents.IsSubagent(path))
            {
                continue;
            }
            // One unreadable transcript is one malformed entry, not the end of the report.
            if (Transcript.Read(path) is not { } parsed)
            {
                report.Malformed++;
                continue;
            }
            report.Compact += parsed.Compactions.Count;
            report.SessionStems.Add(Path.GetFileNameWithoutExtension(path));
            c.Session(parsed);
            parents.Add(new Subagents.Parent(path, parsed));
        }
        report.NoCheckpoint = report.Sessions;
        report.Tools.Finish();
        report.BashFamilies.Finish();
        report.McpGroups.Finish();
        foreach (var (name, row) in report.Skills ?? [])
        {
            row.Mean = row.Count == 0 ? 0 : row.Bytes / row.Count;
            row.P95 = c._skillSamples.P95Of(name, row.Max);
        }
        var denom = report.UsageCacheRead + report.UsageCacheCreate + report.UsageInput;
        report.CacheHitRate = denom == 0 ? 0 : (double)report.UsageCacheRead / denom;
        c._finals.Sort();
        report.MedianFinalContext = c._finals.Count == 0 ? 0 : c._finals[c._finals.Count / 2];
        report.Subagents = Subagents.Collect(parents, cutoff);
        // The `subagent` repeat-read class: invisible to a single session's fold.
        if (report.Subagents is { } sa)
        {
            report.RepeatReads.Subagent.Calls += sa.RereadCalls;
            report.RepeatReads.Subagent.Bytes += sa.RereadBytes;
        }
        return report;
    }

    private void Session(Transcript t)
    {
        var r = _report;
        r.Sessions++;
        r.Lines += t.Lines;
        r.Malformed += t.Malformed;
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var families = new Dictionary<string, string>(StringComparer.Ordinal);
        var skills = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var u in t.ToolUses)
        {
            names[u.Id] = u.Name;
            if (u.Name is "Bash" && Json.Text(u.Input, "command") is { } cmd)
            {
                families[u.Id] = BashFamily.Of(cmd, _signals.VisibleProgram);
            }
            if (u.Name is "Skill" && Json.Text(u.Input, "skill") is { } skill)
            {
                skills[u.Id] = skill;
            }
            StatsFolds.Edits(r.Edits, u);
        }
        StatsFolds.ReadDelta(r.ReadDelta, t);
        StatsFolds.ReadWhole(r.ReadWhole, t, _replay, _signals);
        StatsFolds.ReadDeny(r.ReadDeny, t, _signals);
        StatsFolds.Repeat(r.Repeat, t);
        StatsFolds.RepeatReads(r.RepeatReads, t, _signals);
        StatsFolds.ExpandAfter(r.ExpandAfter, t);
        Results(t, names, families);
        r.Thinking.Blocks += t.Thinking.Count;
        r.Thinking.Bytes += t.Thinking.Sum(b => b.Bytes);
        Images(t, names);
        r.Dictionary.Fold(t);
        Skills(t, skills);
        foreach (var u in t.Usages)
        {
            r.UsageInput += u.Input;
            r.UsageCacheCreate += u.CacheCreate;
            r.UsageCacheRead += u.CacheRead;
            r.UsageOutput += u.Output;
        }
        if (t.Usages.Count > 0 && t.Usages[^1] is var last)
        {
            _finals.Add(last.Input + last.CacheCreate + last.CacheRead);
        }
    }

    /// <summary>Each tool result into the tool, Bash family and MCP group rows, with its archive replay.</summary>
    private void Results(
        Transcript t,
        Dictionary<string, string> names,
        Dictionary<string, string> families
    )
    {
        var r = _report;
        var n = (long)t.Turns;
        foreach (var res in t.ToolResults)
        {
            var name = names.GetValueOrDefault(res.ToolUseId) ?? "unknown";
            if (
                _plugin.Length > 0
                && !name.Equals(_plugin, StringComparison.Ordinal)
                && !name.Contains(_plugin, StringComparison.Ordinal)
            )
            {
                continue;
            }
            var bytes = Transcript.Utf8(res.Content);
            var tokens = Calls.EstTokens(bytes);
            var remain = Math.Max(n - res.Turn, 0);
            var ctt = SizeRows.SatMul(tokens, remain);
            var after = StatsFolds.ReplayCtt(res.Content, tokens, remain, _replay, _signals);
            r.CttTotal = SizeRows.Sat(r.CttTotal, ctt);
            r.CttArchive = SizeRows.Sat(r.CttArchive, after);
            if (after != ctt)
            {
                r.ArchiveCandidates++;
            }
            r.Tools.Add(name, bytes, tokens, ctt);
            if (name is "Bash")
            {
                r.BashFamilies.Add(
                    families.GetValueOrDefault(res.ToolUseId) ?? "other",
                    bytes,
                    tokens,
                    ctt
                );
            }
            if (StatsFolds.McpGroup(name) is { } group)
            {
                r.McpGroups.Add(group, bytes, tokens, ctt);
            }
        }
    }

    /// <summary>Image blocks; each one's tokens ride every request at or after its turn.</summary>
    private void Images(Transcript t, Dictionary<string, string> names)
    {
        var row = _report.Images;
        foreach (var img in t.Images)
        {
            long later = t.Usages.Count(u => u.Turn >= img.Turn);
            row.Blocks++;
            row.Bytes += img.Bytes;
            row.Tokens += img.Tokens;
            row.Resident = SizeRows.Sat(row.Resident, SizeRows.SatMul(img.Tokens, later));
            row.NoSize += img.Sized ? 0 : 1;
            var source =
                img.ToolUseId.Length == 0
                    ? "user"
                    : names.GetValueOrDefault(img.ToolUseId) ?? "unknown";
            if (!row.BySource.TryGetValue(source, out var s))
            {
                s = new ImageSource();
                row.BySource[source] = s;
            }
            s.Blocks++;
            s.Bytes += img.Bytes;
            s.Tokens += img.Tokens;
        }
    }

    /// <summary>
    /// Injected skill bodies, keyed by the <c>Skill</c> call's <c>input.skill</c>; resident is
    /// body bytes times the requests at or after the injection.
    /// </summary>
    private void Skills(Transcript t, Dictionary<string, string> skills)
    {
        if (t.Injected.Count == 0)
        {
            return;
        }
        var map = _report.Skills ??= new(StringComparer.Ordinal);
        foreach (var inj in t.Injected)
        {
            if (!skills.TryGetValue(inj.ToolUseId, out var name))
            {
                continue;
            }
            long later = t.Usages.Count(u => u.Turn >= inj.Turn);
            if (!map.TryGetValue(name, out var row))
            {
                row = new SkillRow();
                map[name] = row;
            }
            row.Count++;
            row.Bytes += inj.Bytes;
            row.Max = Math.Max(row.Max, inj.Bytes);
            row.EstTokens += Calls.EstTokens(inj.Bytes);
            row.Resident = SizeRows.Sat(row.Resident, SizeRows.SatMul(inj.Bytes, later));
            _skillSamples.Sample(name, inj.Bytes);
        }
    }
}
