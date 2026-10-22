using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using static Rtok.Measure.StatsReport;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok stats --json</c>: the report's fields in rtok's order, snake_case. A row that is empty
/// stays out, so a report without it reads as it did before the row existed.
/// </summary>
internal static class StatsJson
{
    public static string Render(StatsReport r)
    {
        using var stream = new MemoryStream();
        using (
            var w = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            )
        )
        {
            w.WriteStartObject();
            Counts(w, r);
            Rows(w, r);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Counts(Utf8JsonWriter w, StatsReport r)
    {
        w.WriteNumber("sessions", r.Sessions);
        NonZero(w, "compact", r.Compact);
        NonZero(w, "checkpoint", r.Checkpoint);
        NonZero(w, "no_checkpoint", r.NoCheckpoint);
        w.WriteNumber("lines", r.Lines);
        w.WriteNumber("malformed", r.Malformed);
        Sizes(w, "tools", r.Tools);
        Sizes(w, "bash_families", r.BashFamilies);
        if (r.BashFilter.Count > 0)
        {
            w.WriteStartObject("bash_filter");
            foreach (var (k, v) in r.BashFilter)
            {
                w.WriteString(k, v);
            }
            w.WriteEndObject();
        }
        if (r.BashDefaultRule.Rows.Count > 0)
        {
            Sizes(w, "bash_default_rule", r.BashDefaultRule);
        }
        Sizes(w, "mcp_groups", r.McpGroups);
        w.WriteNumber("usage_input", r.UsageInput);
        w.WriteNumber("usage_cache_create", r.UsageCacheCreate);
        w.WriteNumber("usage_cache_read", r.UsageCacheRead);
        w.WriteNumber("usage_output", r.UsageOutput);
        w.WriteNumber("cache_hit_rate", r.CacheHitRate);
        w.WriteNumber("median_final_context", r.MedianFinalContext);
        w.WriteNumber("ctt_total", r.CttTotal);
        w.WriteNumber("ctt_archive", r.CttArchive);
        w.WriteNumber("archive_candidates", r.ArchiveCandidates);
        Apis(w, "api", r.Api, always: true);
        Apis(w, "lanes", r.Lanes, always: false);
        Apis(w, "lane_tiers", r.LaneTiers, always: false);
        if (r.Cost is { } cost)
        {
            Cost(w, cost);
        }
    }

    private static void Rows(Utf8JsonWriter w, StatsReport r)
    {
        Obj(
            w,
            "edits",
            r.Edits.Calls > 0,
            ("calls", r.Edits.Calls),
            ("old_bytes", r.Edits.OldBytes),
            ("new_bytes", r.Edits.NewBytes),
            ("tool_input_bytes", r.Edits.ToolInputBytes)
        );
        Obj(
            w,
            "read_delta",
            r.ReadDelta.Calls > 0,
            ("calls", r.ReadDelta.Calls),
            ("bytes", r.ReadDelta.Bytes),
            ("read_bytes", r.ReadDelta.ReadBytes)
        );
        var rw = r.ReadWhole;
        Obj(
            w,
            "read_whole",
            rw.Calls > 0,
            ("calls", rw.Calls),
            ("bytes", rw.Bytes),
            ("edited", rw.Edited),
            ("edited_bytes", rw.EditedBytes),
            ("read_bytes", rw.ReadBytes),
            ("result_bytes", rw.ResultBytes)
        );
        var rd = r.ReadDeny;
        Obj(
            w,
            "read_deny",
            rd.Calls > 0,
            ("calls", rd.Calls),
            ("tokens", rd.Tokens),
            ("then_mcp", rd.ThenMcp),
            ("then_native", rd.ThenNative),
            ("then_toolsearch", rd.ThenToolSearch)
        );
        ResultRows(w, r);
        Tail(w, r);
    }

    private static void ResultRows(Utf8JsonWriter w, StatsReport r)
    {
        Obj(
            w,
            "repeat",
            r.Repeat.Calls > 0,
            ("calls", r.Repeat.Calls),
            ("bytes", r.Repeat.Bytes),
            ("result_bytes", r.Repeat.ResultBytes)
        );
        var ea = r.ExpandAfter;
        Obj(
            w,
            "expand_after",
            ea.Calls > 0,
            ("calls", ea.Calls),
            ("bytes", ea.Bytes),
            ("shown_bytes", ea.ShownBytes)
        );
        var bu = r.BashUnmarked;
        Obj(
            w,
            "bash_unmarked",
            bu.GateCalls > 0 || bu.UnmatchedCalls > 0,
            ("gate_calls", bu.GateCalls),
            ("gate_bytes", bu.GateBytes),
            ("unmatched_calls", bu.UnmatchedCalls),
            ("unmatched_bytes", bu.UnmatchedBytes)
        );
        Obj(
            w,
            "thinking",
            r.Thinking.Blocks > 0,
            ("blocks", r.Thinking.Blocks),
            ("bytes", r.Thinking.Bytes)
        );
    }

    /// <summary>Images, dictionary, skills, sub-agents and repeat reads: each only when it has rows.</summary>
    private static void Tail(Utf8JsonWriter w, StatsReport r)
    {
        Images(w, r.Images);
        Dictionary(w, r.Dictionary);
        if (r.Skills is { } skills)
        {
            w.WriteStartObject("skills");
            foreach (var (name, s) in skills)
            {
                Obj(
                    w,
                    name,
                    true,
                    ("count", s.Count),
                    ("bytes", s.Bytes),
                    ("mean", s.Mean),
                    ("p95", s.P95),
                    ("max", s.Max),
                    ("est_tokens", s.EstTokens),
                    ("resident", s.Resident)
                );
            }
            w.WriteEndObject();
        }
        if (r.Subagents is { } sa)
        {
            Subagents(w, sa);
        }
        if (!r.RepeatReads.IsEmpty)
        {
            w.WriteStartObject("repeat_reads");
            foreach (var (name, c) in r.RepeatReads.Classes)
            {
                Obj(w, name, true, ("calls", c.Calls), ("bytes", c.Bytes));
            }
            w.WriteEndObject();
        }
    }

    private static void Images(Utf8JsonWriter w, ImageRow d)
    {
        if (d.Blocks == 0)
        {
            return;
        }
        w.WriteStartObject("images");
        Fields(
            w,
            ("blocks", d.Blocks),
            ("bytes", d.Bytes),
            ("tokens", d.Tokens),
            ("resident", d.Resident),
            ("no_size", d.NoSize)
        );
        w.WriteStartObject("by_source");
        foreach (var (src, x) in d.BySource)
        {
            Obj(w, src, true, ("blocks", x.Blocks), ("bytes", x.Bytes), ("tokens", x.Tokens));
        }
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void Dictionary(Utf8JsonWriter w, DictionaryRow d)
    {
        if (d.IsEmpty)
        {
            return;
        }
        w.WriteStartObject("dictionary");
        Fields(
            w,
            ("requests", d.Requests),
            ("input_bytes", d.InputBytes),
            ("request_tokens", d.RequestTokens)
        );
        Obj(w, "paths", true, ("repeated", d.Paths.Repeated), ("saved", d.Paths.Saved));
        Obj(w, "idents", true, ("repeated", d.Idents.Repeated), ("saved", d.Idents.Saved));
        w.WriteEndObject();
    }

    private static void Subagents(Utf8JsonWriter w, Subagents sa)
    {
        w.WriteStartObject("subagents");
        Fields(
            w,
            ("sessions", sa.Sessions),
            ("count", sa.Count),
            ("result_bytes", sa.ResultBytes),
            ("parent_result_bytes", sa.ParentResultBytes),
            ("read_bytes", sa.ReadBytes),
            ("read_parent", sa.ReadParent),
            ("read_sibling", sa.ReadSibling),
            ("reread_bytes", sa.RereadBytes),
            ("reread_calls", sa.RereadCalls)
        );
        w.WriteNumber("share_read", sa.ShareRead);
        w.WriteNumber("share_results", sa.ShareResults);
        w.WriteNumber("share_tree", sa.ShareTree);
        Fields(
            w,
            ("usage_input", sa.UsageInput),
            ("usage_cache_read", sa.UsageCacheRead),
            ("usage_cache_write", sa.UsageCacheWrite),
            ("usage_output", sa.UsageOutput)
        );
        w.WriteStartArray("by_type");
        foreach (var t in sa.ByType)
        {
            w.WriteStartObject();
            w.WriteString("agent_type", t.AgentType);
            w.WriteString("model", t.Model);
            Fields(
                w,
                ("count", t.Count),
                ("result_bytes", t.ResultBytes),
                ("read_bytes", t.ReadBytes),
                ("reread_bytes", t.RereadBytes)
            );
            w.WriteEndObject();
        }
        w.WriteEndArray();
        foreach (
            var (name, b) in (ReadOnlySpan<(string, Subagents.BriefSplit)>)
                [("with_brief", sa.WithBrief), ("without_brief", sa.WithoutBrief)]
        )
        {
            w.WriteStartObject(name);
            Fields(
                w,
                ("count", b.Count),
                ("read_bytes", b.ReadBytes),
                ("reread_bytes", b.RereadBytes)
            );
            w.WriteNumber("share_read", b.ShareRead);
            w.WriteNumber("usage_input", b.UsageInput);
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static void Cost(Utf8JsonWriter w, CostReport c)
    {
        w.WriteStartObject("cost");
        w.WriteStartObject("models");
        foreach (var (model, m) in c.Models)
        {
            w.WriteStartObject(model);
            Fields(
                w,
                ("input", m.Input),
                ("cache_create", m.CacheCreate),
                ("cache_read", m.CacheRead),
                ("output", m.Output)
            );
            Money(w, "cost", m.Cost);
            Money(w, "saved", m.Saved);
            w.WriteEndObject();
        }
        w.WriteEndObject();
        w.WriteStartArray("unknown");
        c.Unknown.ForEach(w.WriteStringValue);
        w.WriteEndArray();
        w.WriteNumber("total_cost", c.TotalCost);
        w.WriteNumber("total_saved", c.TotalSaved);
        if (c.Lanes.Count > 0)
        {
            w.WriteStartObject("lanes");
            foreach (var (lane, l) in c.Lanes)
            {
                w.WriteStartObject(lane);
                w.WriteNumber("cost", l.Cost);
                w.WriteBoolean("partial", l.Partial);
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static void Money(Utf8JsonWriter w, string name, double? v)
    {
        if (v is { } x)
        {
            w.WriteNumber(name, x);
        }
        else
        {
            w.WriteNull(name);
        }
    }

    private static void Apis(
        Utf8JsonWriter w,
        string name,
        SortedDictionary<string, ApiRow> rows,
        bool always
    )
    {
        if (!always && rows.Count == 0)
        {
            return;
        }
        w.WriteStartObject(name);
        foreach (var (k, a) in rows)
        {
            w.WriteStartObject(k);
            Fields(
                w,
                ("input", a.Input),
                ("cache_create", a.CacheCreate),
                ("cache_read", a.CacheRead),
                ("output", a.Output)
            );
            w.WriteNumber("hit", a.Hit);
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static void Sizes(Utf8JsonWriter w, string name, SizeRows rows)
    {
        w.WriteStartObject(name);
        foreach (var (k, s) in rows.Rows)
        {
            Obj(
                w,
                k,
                true,
                ("count", s.Count),
                ("total_bytes", s.TotalBytes),
                ("mean", s.Mean),
                ("p95", s.P95),
                ("max", s.Max),
                ("est_tokens", s.EstTokens),
                ("ctt", s.Ctt)
            );
        }
        w.WriteEndObject();
    }

    private static void NonZero(Utf8JsonWriter w, string name, long n)
    {
        if (n != 0)
        {
            w.WriteNumber(name, n);
        }
    }

    /// <summary>An object of counters, written only when <paramref name="present"/>.</summary>
    private static void Obj(
        Utf8JsonWriter w,
        string name,
        bool present,
        params ReadOnlySpan<(string Name, long Value)> fields
    )
    {
        if (!present)
        {
            return;
        }
        w.WriteStartObject(name);
        Fields(w, fields);
        w.WriteEndObject();
    }

    private static void Fields(
        Utf8JsonWriter w,
        params ReadOnlySpan<(string Name, long Value)> fields
    )
    {
        foreach (var (n, v) in fields)
        {
            w.WriteNumber(n, v);
        }
    }
}
