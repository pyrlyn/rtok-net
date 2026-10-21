namespace Rtok.Config;

/// <summary>rtok stats</summary>
/// <remarks>Read from [stats].</remarks>
public sealed class StatsSection
{
    /// <summary>window: a date (2026-09-01) or a duration (30d)</summary>
    public string Since { get; set; } = "30d";

    /// <summary>table | json (--json)</summary>
    public string Format { get; set; } = "table";

    /// <summary>"" = all (--plugin &lt;id&gt;)</summary>
    public string Plugin { get; set; } = "";

    /// <summary>Claude Code's session transcripts</summary>
    public string TranscriptsDir { get; set; } = "~/.claude/projects";

    /// <summary>Codex CLI session logs, read as one more `api` row</summary>
    public string CodexDir { get; set; } = "~/.codex/sessions";

    /// <summary>per class (--calibrate)</summary>
    public uint CalibrateSamples { get; set; } = 30;

    /// <summary>default name for --compare; "" = none</summary>
    public string Baseline { get; set; } = "";

    /// <summary>show per-model USD costs (--price)</summary>
    public bool Price { get; set; }

    /// <summary>USD per MTok per model id (`rtok stats --price`, )</summary>
    public IDictionary<string, ModelPrice> Prices { get; set; } = ModelPrice.Defaults();
}
