namespace Rtok.Config;

/// <summary>`rtok task export github`: mirror the disk tasks into GitHub Issues, one way</summary>
/// <remarks>Read from [tasks.export.github].</remarks>
public sealed class TasksExportGithubSection
{
    /// <summary>owner/name; empty: [tasks.github] repo, else the origin remote</summary>
    public string Repo { get; set; } = "";
}
