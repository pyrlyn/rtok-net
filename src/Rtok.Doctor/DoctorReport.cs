using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>Everything <c>rtok doctor</c> finds, as <c>--json</c> prints it (snake_case keys).</summary>
public sealed record DoctorReport
{
    /// <summary>Claude Code's command hooks across its settings files and enabled plugins.</summary>
    public int HooksTotal { get; init; }

    /// <summary>Hooks per event, sorted by event.</summary>
    public required SortedDictionary<string, int> HooksByEvent { get; init; }

    /// <summary>Hooks per event and tool matcher; hooks without a matcher count under their event only.</summary>
    public required SortedDictionary<
        string,
        SortedDictionary<string, int>
    > HooksByTool { get; init; }

    /// <summary>The MCP servers Claude Code starts, with their tool counts and description tokens.</summary>
    public required IReadOnlyList<McpServerRow> Mcp { get; init; }

    /// <summary>The Anthropic base-URL chain: <c>8790→api.anthropic.com</c>; empty when unset.</summary>
    public required string Proxy { get; init; }

    /// <summary>The OpenAI base-URL chain; empty when unset.</summary>
    public required string ProxyOpenai { get; init; }

    /// <summary>Claude Code may not defer MCP tools: tool search is not known to be on.</summary>
    public bool McpToolSearchDisabled { get; init; }

    /// <summary>Whether Claude Code defers MCP tool definitions, and what says so.</summary>
    public required ToolSearch McpToolSearch { get; init; }

    /// <summary>The shell's <c>BASH_MAX_OUTPUT_LENGTH</c>; null when unset.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? BashMaxOutputLength { get; init; }

    /// <summary><c>autoCompactWindow</c> from the user settings as JSON; null when unset.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? AutoCompactWindow { get; init; }

    /// <summary>The instruction audit; null unless <c>[doctor] instructions</c> or <c>--instructions</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public InstructionAudit? Instructions { get; init; }

    /// <summary>The skills every request lists; null when there are none.</summary>
    public SkillsAudit? Skills { get; init; }

    /// <summary>Where rtok and a host or another tool do the same job, and what turns rtok's side off.</summary>
    public IReadOnlyList<string>? Overlaps { get; init; }

    /// <summary>The <c>[proxy.tools_rewrite]</c> hint when MCP descriptions cost much and rtok's proxy is on the way.</summary>
    public string? ToolsRewriteAdvice { get; init; }

    /// <summary>Every host app rtok knows that is installed here, and rtok's modules in it.</summary>
    public required IReadOnlyList<AgentRow> Agents { get; init; }

    /// <summary>What rtok exposes wider than it should: binds, tokens and file modes.</summary>
    public IReadOnlyList<string>? Exposure { get; init; }

    /// <summary>Broken, suspect, unverified and duplicate hooks, duplicate MCP servers, unreadable files.</summary>
    public IReadOnlyList<Problem>? Problems { get; init; }

    /// <summary>How many problems <c>--fix</c> would remove by default.</summary>
    public int Fixable { get; init; }
}
