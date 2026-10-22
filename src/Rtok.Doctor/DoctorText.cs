using System.Globalization;
using System.Text;

namespace Rtok.Doctor;

/// <summary>The report as <c>rtok doctor</c> prints it, every line ending in <c>\n</c>.</summary>
public static class DoctorText
{
    private static readonly (string Kind, string Title)[] HookClasses =
    [
        (ProblemKinds.BrokenHook, "broken"),
        (ProblemKinds.SuspectHook, "suspect"),
        (ProblemKinds.UnverifiedHook, "cannot verify"),
        (ProblemKinds.StalePlugin, "stale plugin"),
        (ProblemKinds.UnreadableConfig, "unreadable"),
    ];

    /// <summary>Renders the report.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The text.</returns>
    public static string Render(DoctorReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        var problems = report.Problems ?? [];
        Line(sb, $"hooks {report.HooksTotal}");
        foreach (var (ev, n) in report.HooksByEvent)
        {
            Line(sb, $"  {ev} {n}");
            foreach (var (tool, m) in report.HooksByTool.GetValueOrDefault(ev) ?? [])
            {
                Line(sb, $"    {tool} {m}");
            }
        }
        HookChecks(sb, problems);
        Duplicates(
            sb,
            "duplicate hooks",
            problems.Where(p => p.Kind is ProblemKinds.DuplicateHook)
        );
        Duplicates(
            sb,
            "duplicate mcp servers",
            problems.Where(p =>
                p.Kind
                    is ProblemKinds.DuplicateMcp
                        or ProblemKinds.ConflictingMcp
                        or ProblemKinds.OwnMcp
            )
        );
        Line(sb, "mcp");
        foreach (var s in report.Mcp)
        {
            Line(sb, $"  {s.Name} ({s.Tools} tools, ~{s.DescTokens} desc tokens) {s.Cmd}");
        }
        Line(sb, $"proxy {Chain(report.Proxy)}");
        Line(sb, $"proxy openai {Chain(report.ProxyOpenai)}");
        if (report.McpToolSearch.Source is { } source)
        {
            Line(sb, $"mcp_tool_search {report.McpToolSearch.State} ({source})");
        }
        Line(sb, $"BASH_MAX_OUTPUT_LENGTH {report.BashMaxOutputLength ?? "(unset)"}");
        Line(sb, $"autoCompactWindow {report.AutoCompactWindow ?? "(unset)"}");
        Agents(sb, report.Agents);
        Instructions(sb, report.Instructions);
        Section(sb, "overlaps", report.Overlaps);
        Skills(sb, report.Skills);
        Section(sb, "exposure", report.Exposure);
        if (report.Fixable > 0)
        {
            Line(
                sb,
                $"{report.Fixable} {(report.Fixable == 1 ? "problem" : "problems")} can be fixed: run rtok doctor --fix"
            );
        }
        return sb.ToString();
    }

    private static string Chain(string chain) => chain.Length == 0 ? "none" : chain;

    private static void HookChecks(StringBuilder sb, IReadOnlyList<Problem> problems)
    {
        var found = problems
            .Where(p =>
                HookClasses.Any(c => string.Equals(c.Kind, p.Kind, StringComparison.Ordinal))
            )
            .ToList();
        Line(sb, found.Count == 0 ? "hooks check none found" : "hooks check");
        foreach (var (kind, title) in HookClasses)
        {
            foreach (
                var p in found.Where(p => string.Equals(p.Kind, kind, StringComparison.Ordinal))
            )
            {
                if (kind is ProblemKinds.StalePlugin or ProblemKinds.UnreadableConfig)
                {
                    Line(sb, $"  {title} {p.Agent} {p.Detail}");
                    continue;
                }
                var matcher = p.Matcher is null ? "" : $"[{p.Matcher}]";
                var note = p.Fixable ? " (can be cleaned up)" : "";
                Line(sb, $"  {title} {p.Agent} {p.Event}{matcher} `{p.Command}`: {p.Detail}{note}");
                Line(sb, $"    {p.Source} {p.Path}");
            }
        }
    }

    /// <summary>Each group once, with its head's detail up to the first <c>;</c>, then every copy and its role.</summary>
    private static void Duplicates(StringBuilder sb, string title, IEnumerable<Problem> problems)
    {
        var list = problems.ToList();
        if (list.Count == 0)
        {
            Line(sb, $"{title} none found");
            return;
        }
        Line(sb, title);
        foreach (var group in list.GroupBy(p => (p.Kind, p.Group)))
        {
            var head = group.First();
            var ev = string.IsNullOrEmpty(head.Event) ? "" : head.Event + " ";
            var matcher = head.Matcher is null ? "" : $"[{head.Matcher}] ";
            var cut = head.Detail.IndexOf(';', StringComparison.Ordinal);
            Line(
                sb,
                $"  {head.Agent} {ev}{matcher}`{head.Command}`: {(cut < 0 ? head.Detail : head.Detail[..cut])}"
            );
            foreach (var p in group)
            {
                var role = p.Kind switch
                {
                    ProblemKinds.ConflictingMcp => "other",
                    _ when p.Keep => "keep ",
                    ProblemKinds.OwnMcp => "info ",
                    _ => "extra",
                };
                Line(sb, $"    {role} {p.Source} {p.Path}");
            }
        }
    }

    private static void Agents(StringBuilder sb, IReadOnlyList<AgentRow> agents)
    {
        if (agents.Count == 0)
        {
            return;
        }
        Line(sb, "agents");
        foreach (var a in agents)
        {
            Line(sb, $"  {a.Host} ({a.Kind})");
            foreach (var m in a.Modules)
            {
                Line(sb, $"    {m.Name, -7} {m.State.Replace('_', ' ')}{m.Note}");
            }
        }
    }

    private static void Instructions(StringBuilder sb, InstructionAudit? audit)
    {
        if (audit is null)
        {
            return;
        }
        Line(sb, "instructions");
        foreach (var r in audit.Rows)
        {
            Line(sb, $"  {r.Name} {r.Tokens} tokens {r.Path}{(r.Warn ? " WARN" : "")}");
        }
        foreach (var d in audit.Duplicates)
        {
            Line(sb, $"  duplicate `{d.Sentence}` in {string.Join(", ", d.Names)}");
        }
    }

    private static void Skills(StringBuilder sb, SkillsAudit? skills)
    {
        if (skills is null)
        {
            return;
        }
        Line(
            sb,
            $"skills ({skills.Rows.Count} listed, {skills.DescBytes} desc bytes ≈ {skills.DescBytes / 4} tokens per request)"
        );
        var over = skills.Rows.Where(r => r.WarnDesc).ToList();
        if (over.Count > 0)
        {
            var recoverable = over.Sum(r => r.DescChars - SkillScan.DescMax) / 4;
            Line(
                sb,
                $"  descriptions over {SkillScan.DescMax} chars: {over.Count} skills, ≈ {recoverable} tokens/request recoverable"
            );
        }
        foreach (var r in skills.Rows)
        {
            var flags =
                (r.WarnDesc ? " WARN desc>120" : "")
                + (r.WarnBody ? " WARN body>8K (references/)" : "");
            Line(sb, $"  {r.Name} {r.Source} desc {r.DescChars}c body {r.BodyBytes}B{flags}");
        }
        foreach (var d in skills.Duplicates ?? [])
        {
            var bodies = d.Identical ? "identical" : "differ";
            Line(
                sb,
                $"  WARN duplicate skill `{d.Name}`: {d.Copies.Count} listings, bodies {bodies}, extra listings ≈ {d.ExtraTokens} tokens per request"
            );
            foreach (var c in d.Copies)
            {
                Line(sb, $"    {c.Source} {c.Dir}");
            }
        }
    }

    private static void Section(StringBuilder sb, string title, IReadOnlyList<string>? lines)
    {
        if (lines is null or { Count: 0 })
        {
            return;
        }
        Line(sb, title);
        foreach (var l in lines)
        {
            Line(sb, $"  {l}");
        }
    }

    private static void Line(StringBuilder sb, string text) =>
        sb.Append(CultureInfo.InvariantCulture, $"{text}\n");
}
