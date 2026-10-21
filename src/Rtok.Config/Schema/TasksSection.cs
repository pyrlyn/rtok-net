namespace Rtok.Config;

/// <summary>task adapters; usually set per project in .rtok.toml</summary>
/// <remarks>Read from [tasks].</remarks>
public sealed class TasksSection
{
    /// <summary>disk | github | gitlab</summary>
    public string Adapter { get; set; } = "disk";

    /// <summary>task id prefix (R → R12); empty: first letter of the project name</summary>
    public string Prefix { get; set; } = "";

    /// <summary>The [tasks.disk] table.</summary>
    public TasksDiskSection Disk { get; set; } = new();

    /// <summary>The [tasks.github] table.</summary>
    public TasksGithubSection Github { get; set; } = new();

    /// <summary>The [tasks.gitlab] table.</summary>
    public TasksGitlabSection Gitlab { get; set; } = new();

    /// <summary>The [tasks.export] table.</summary>
    public TasksExportSection Export { get; set; } = new();
}
