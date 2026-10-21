namespace Rtok.Config;

/// <summary>`rtok task export linear`: mirror the disk tasks into Linear, one way; token in LINEAR_API_KEY</summary>
/// <remarks>Read from [tasks.export.linear].</remarks>
public sealed class TasksExportLinearSection
{
    /// <summary>the team's short key, e.g. "ENG"; empty: the export refuses to run</summary>
    public string Team { get; set; } = "";
}
