using System.Globalization;
using System.Text;
using Rtok.Agents.Usage;
using static Rtok.Measure.StatsReport;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok stats</c> as text: one summary line per row that has anything, then the size tables.
/// Column widths are floors, so a wide name widens its column instead of breaking it.
/// </summary>
internal static class StatsTable
{
    private static readonly int[] SizeFloors = [24, 7, 12, 8, 8, 8, 12, 12];
    private static readonly int[] BashFloors = [24, 8, 7, 12, 8, 8, 8, 12, 12];
    private static readonly int[] SkillFloors = [24, 7, 12, 8, 8, 8, 12, 14];
    private static readonly int[] ApiFloors = [24, 8, 12, 10, 6, 6];
    private static readonly int[] CostFloors = [24, 12, 12, 12, 12, 10, 10];

    public static string Render(StatsReport r)
    {
        var s = new StringBuilder();
        s.Append(
            Inv(
                $"sessions {r.Sessions}  compact {r.Compact}  checkpoint {r.Checkpoint}  no_checkpoint {r.NoCheckpoint}  lines {r.Lines}  malformed {r.Malformed}\n"
            )
        );
        s.Append(
            Inv(
                $"usage input={r.UsageInput} cache_create={r.UsageCacheCreate} cache_read={r.UsageCacheRead} output={r.UsageOutput}  hit={r.CacheHitRate * 100:F1}%  median_context={r.MedianFinalContext}\n"
            )
        );
        if (r.Subagents is { } sa)
        {
            SubagentLines(s, sa);
        }
        s.Append(ApiTable("api", r.Api));
        s.Append(ApiTable("lane", r.Lanes));
        s.Append(ApiTable("lane/tier", r.LaneTiers));
        if (r.CttTotal > 0)
        {
            var pct = 100.0 * (r.CttTotal - (double)r.CttArchive) / r.CttTotal;
            s.Append(
                Inv(
                    $"archive replay (estimate) ctt {r.CttTotal} → {r.CttArchive}  -{pct:F1}%  over {r.ArchiveCandidates} results\n"
                )
            );
        }
        if (r.Cost is { } cost)
        {
            s.Append(CostTable(cost));
        }
        CallLines(s, r);
        s.Append(SizeTable("tool", r.Tools));
        s.Append(BashTable(r.BashFamilies, r.BashFilter));
        if (r.BashDefaultRule.Rows.Count > 0)
        {
            s.Append(SizeTable("bash_default", r.BashDefaultRule));
        }
        if (r.BashUnmarked is { GateCalls: > 0 } or { UnmatchedCalls: > 0 })
        {
            var d = r.BashUnmarked;
            var total = d.GateBytes + d.UnmatchedBytes;
            s.Append(
                Inv(
                    $"bash unmarked  below_gate calls {d.GateCalls} bytes {d.GateBytes} {Calls.Pct(d.GateBytes, total):F1}%  no_rule calls {d.UnmatchedCalls} bytes {d.UnmatchedBytes} {Calls.Pct(d.UnmatchedBytes, total):F1}%\n"
                )
            );
        }
        s.Append(SizeTable("mcp", r.McpGroups));
        if (r.Skills is { } skills)
        {
            s.Append(SkillTable(skills));
        }
        return s.ToString();
    }

    private static void SubagentLines(StringBuilder s, Subagents sa)
    {
        s.Append(
            Inv(
                $"sub-agents {sa.Sessions} sessions / {sa.Count} agents  results {sa.ResultBytes} B vs parents {sa.ParentResultBytes} B ({Calls.Pct(sa.ResultBytes, sa.ResultBytes + sa.ParentResultBytes):F1}% of the tree)\n"
            )
        );
        s.Append(
            Inv(
                $"  reads {sa.ReadBytes} B ({Calls.Pct(sa.ReadBytes, sa.ResultBytes):F1}% of results)  re-reads: parent {sa.ReadParent} B, sibling {sa.ReadSibling} B\n"
            )
        );
        s.Append(
            Inv(
                $"  re-read {sa.RereadBytes} B = {sa.ShareRead:F1}% of reads, {sa.ShareResults:F1}% of results, {sa.ShareTree:F1}% of the tree\n"
            )
        );
        s.Append(
            Inv(
                $"  usage input={sa.UsageInput} cache_read={sa.UsageCacheRead} cache_write={sa.UsageCacheWrite} output={sa.UsageOutput}\n"
            )
        );
        if (sa.WithBrief.Count > 0 || sa.WithoutBrief.Count > 0)
        {
            foreach (
                var (label, b) in (ReadOnlySpan<(string, Subagents.BriefSplit)>)
                    [("brief", sa.WithBrief), ("no brief", sa.WithoutBrief)]
            )
            {
                s.Append(
                    Inv(
                        $"  {label}: {b.Count} agents  re-read {b.RereadBytes} B ({b.ShareRead:F1}% of reads)  input={b.UsageInput}\n"
                    )
                );
            }
        }
        foreach (var t in sa.ByType)
        {
            s.Append(
                Inv(
                    $"  {t.AgentType} | {t.Model}  {t.Count} agents  results {t.ResultBytes} B  reads {t.ReadBytes} B  re-read {t.RereadBytes} B\n"
                )
            );
        }
    }

    /// <summary>The one-line rows about calls: edits, reads, repeats, thinking, images, dictionary, expands.</summary>
    private static void CallLines(StringBuilder s, StatsReport r)
    {
        if (r.Edits.Calls > 0)
        {
            var e = r.Edits;
            s.Append(
                Inv(
                    $"edit calls {e.Calls}  old_string {e.OldBytes} B  new_string {e.NewBytes} B  old/tool_input {Calls.Pct(e.OldBytes, e.ToolInputBytes):F1}%  old/output_tokens (est) {Calls.Pct(Calls.EstTokens(e.OldBytes), r.UsageOutput):F1}%\n"
                )
            );
        }
        if (r.ReadDelta.Calls > 0)
        {
            var d = r.ReadDelta;
            s.Append(
                Inv(
                    $"read delta calls {d.Calls}  bytes {d.Bytes}  of Read bytes {d.ReadBytes}  {Calls.Pct(d.Bytes, d.ReadBytes):F1}%\n"
                )
            );
        }
        if (r.ReadWhole.Calls > 0)
        {
            var d = r.ReadWhole;
            s.Append(
                Inv(
                    $"read whole calls {d.Calls}  bytes {d.Bytes}  {Calls.Pct(d.Bytes, d.ReadBytes):F1}% of Read  {Calls.Pct(d.Bytes, d.ResultBytes):F1}% of results  not edited {d.Calls - d.Edited} bytes {Calls.Pct(d.Bytes - d.EditedBytes, d.ResultBytes):F1}% of results  edited after {d.Edited}\n"
                )
            );
        }
        if (r.ReadDeny.Calls > 0)
        {
            var d = r.ReadDeny;
            s.Append(
                Inv(
                    $"read deny calls {d.Calls}  tokens {d.Tokens}  then rtok read {d.ThenMcp}  then native Read {d.ThenNative}  then ToolSearch {d.ThenToolSearch}\n"
                )
            );
        }
        if (!r.RepeatReads.IsEmpty)
        {
            s.Append(
                Inv($"repeat reads  total {r.RepeatReads.Classes.Sum(c => c.Counts.Calls)}\n")
            );
            foreach (var (name, c) in r.RepeatReads.Classes)
            {
                s.Append(Inv($"  {name} calls {c.Calls} bytes {c.Bytes}\n"));
            }
        }
        InputShares(s, r);
        RepeatLines(s, r);
    }

    private static void RepeatLines(StringBuilder s, StatsReport r)
    {
        if (r.Repeat.Calls > 0)
        {
            var d = r.Repeat;
            s.Append(
                Inv(
                    $"repeat calls {d.Calls}  bytes {d.Bytes}  of result bytes {d.ResultBytes}  {Calls.Pct(d.Bytes, d.ResultBytes):F1}%\n"
                )
            );
        }
        if (r.ExpandAfter.Calls > 0)
        {
            var d = r.ExpandAfter;
            s.Append(
                Inv(
                    $"expand right after  calls {d.Calls}  bytes {d.Bytes}  shown bytes {d.ShownBytes}\n"
                )
            );
        }
    }

    /// <summary>Thinking, images and the dictionary: shares of the session input.</summary>
    private static void InputShares(StringBuilder s, StatsReport r)
    {
        // Session input: uncached, cache writes and cache reads.
        var input = r.UsageInput + r.UsageCacheCreate + r.UsageCacheRead;
        if (r.Thinking.Blocks > 0)
        {
            var est = Calls.EstTokens(r.Thinking.Bytes);
            var share = input > 0 ? 100.0 * est / input : 0;
            s.Append(
                Inv(
                    $"thinking blocks {r.Thinking.Blocks} bytes {r.Thinking.Bytes} est. tokens {est} {share:F4}% of session input\n"
                )
            );
        }
        if (r.Images.Blocks > 0)
        {
            var d = r.Images;
            s.Append(
                Inv(
                    $"images blocks {d.Blocks}  bytes {d.Bytes}  est. tokens {d.Tokens}  resident {d.Resident}  {Calls.Pct(d.Resident, input):F2}% of session input  no_size {d.NoSize}\n"
                )
            );
            foreach (var (src, x) in d.BySource)
            {
                s.Append(
                    Inv(
                        $"  images {src}  blocks {x.Blocks}  bytes {x.Bytes}  est. tokens {x.Tokens}\n"
                    )
                );
            }
        }
        if (!r.Dictionary.IsEmpty)
        {
            var d = r.Dictionary;
            // Billed tokens back to bytes, so a byte saving compares with what the provider counted.
            var billed = SizeRows.SatMul(d.RequestTokens, 4);
            s.Append(
                Inv(
                    $"dictionary requests {d.Requests}  content bytes {d.InputBytes}  billed input est. bytes {billed}\n"
                )
            );
            foreach (
                var (name, k) in (ReadOnlySpan<(string, DictionaryRow.Kind)>)
                    [("paths", d.Paths), ("idents", d.Idents)]
            )
            {
                s.Append(
                    Inv(
                        $"  {name} repeated {k.Repeated} ({Calls.Pct(k.Repeated, billed):F2}% of billed)  cache-safe saving {k.Saved} ({Calls.Pct(k.Saved, billed):F2}% of billed, {Calls.Pct(k.Saved, d.InputBytes):F2}% of content)\n"
                    )
                );
            }
        }
    }

    private static string SizeTable(string title, SizeRows rows)
    {
        List<IReadOnlyList<string>> lines =
        [
            [title, "count", "bytes", "mean", "p95", "max", "est_tokens", "ctt"],
        ];
        lines.AddRange(
            rows.Rows.Select(kv => (IReadOnlyList<string>)[kv.Key, .. Numbers(kv.Value)])
        );
        return TextTable.Render(Right(SizeFloors.Length, 1), lines, SizeFloors);
    }

    private static string BashTable(SizeRows rows, SortedDictionary<string, string> kinds)
    {
        List<IReadOnlyList<string>> lines =
        [
            ["bash", "filter", "count", "bytes", "mean", "p95", "max", "est_tokens", "ctt"],
        ];
        lines.AddRange(
            rows.Rows.Select(kv =>
                (IReadOnlyList<string>)
                    [kv.Key, kinds.GetValueOrDefault(kv.Key) ?? "default", .. Numbers(kv.Value)]
            )
        );
        return TextTable.Render(Right(BashFloors.Length, 2), lines, BashFloors);
    }

    private static string SkillTable(SortedDictionary<string, SkillRow> skills)
    {
        List<IReadOnlyList<string>> lines =
        [
            ["skill", "count", "bytes", "mean", "p95", "max", "est_tokens", "resident"],
        ];
        lines.AddRange(
            skills.Select(kv =>
                (IReadOnlyList<string>)
                    [
                        kv.Key,
                        .. new[]
                        {
                            kv.Value.Count,
                            kv.Value.Bytes,
                            kv.Value.Mean,
                            kv.Value.P95,
                            kv.Value.Max,
                            kv.Value.EstTokens,
                            kv.Value.Resident,
                        }.Select(Inv),
                    ]
            )
        );
        return TextTable.Render(Right(SkillFloors.Length, 1), lines, SkillFloors);
    }

    /// <summary>The <c>api</c> and lane counters; an empty map prints nothing.</summary>
    private static string ApiTable(string label, SortedDictionary<string, ApiRow> rows)
    {
        if (rows.Count == 0)
        {
            return "";
        }
        List<IReadOnlyList<string>> lines =
        [
            [label, "input", "cache_create", "cache_read", "output", "hit"],
        ];
        lines.AddRange(
            rows.Select(kv =>
                (IReadOnlyList<string>)
                    [
                        kv.Key,
                        Inv(kv.Value.Input),
                        Inv(kv.Value.CacheCreate),
                        Inv(kv.Value.CacheRead),
                        Inv(kv.Value.Output),
                        Inv($"{kv.Value.Hit * 100:F1}%"),
                    ]
            )
        );
        return TextTable.Render(Right(ApiFloors.Length, 1), lines, ApiFloors);
    }

    private static string CostTable(CostReport c)
    {
        var s = new StringBuilder("cost (USD at [stats.prices] $/MTok; `-` = no price row)\n");
        if (c.Models.Count == 0 && c.Unknown.Count == 0)
        {
            return s.Append("  no usage rows\n").ToString();
        }
        static string Money(double? v) => v is { } x ? Inv($"{x:F2}") : "-";
        List<IReadOnlyList<string>> lines =
        [
            ["model", "input", "cache_create", "cache_read", "output", "cost", "saved"],
        ];
        lines.AddRange(
            c.Models.Select(kv =>
                (IReadOnlyList<string>)
                    [
                        kv.Key,
                        Inv(kv.Value.Input),
                        Inv(kv.Value.CacheCreate),
                        Inv(kv.Value.CacheRead),
                        Inv(kv.Value.Output),
                        Money(kv.Value.Cost),
                        Money(kv.Value.Saved),
                    ]
            )
        );
        s.Append(TextTable.Render(Right(CostFloors.Length, 1), lines, CostFloors));
        s.Append(
            Inv(
                $"cost total ${c.TotalCost:F2} (cache reads saved ${c.TotalSaved:F2}; {c.Unknown.Count} model(s) without a price)\n"
            )
        );
        if (c.Lanes.Count > 0)
        {
            s.Append("cost by lane (USD; `*` = excludes models without a price)\n");
            foreach (var (lane, l) in c.Lanes)
            {
                s.Append(Inv($"  {lane, -12} {l.Cost, 10:F2}{(l.Partial ? "*" : "")}\n"));
            }
        }
        return s.ToString();
    }

    private static IEnumerable<string> Numbers(SizeRow r) =>
        new[] { r.Count, r.TotalBytes, r.Mean, r.P95, r.Max, r.EstTokens, r.Ctt }.Select(Inv);

    /// <summary>The first <paramref name="left"/> columns left-aligned, the rest right-aligned.</summary>
    private static bool[] Right(int columns, int left) =>
        [.. Enumerable.Range(0, columns).Select(i => i >= left)];

    private static string Inv(long n) => n.ToString(CultureInfo.InvariantCulture);

    private static string Inv(FormattableString s) => FormattableString.Invariant(s);
}
