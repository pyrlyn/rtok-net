namespace Rtok.Config;

/// <summary>where `rtok task export` mirrors the disk tasks</summary>
/// <remarks>Read from [tasks.export].</remarks>
public sealed class TasksExportSection
{
    /// <summary>The [tasks.export.github] table.</summary>
    public TasksExportGithubSection Github { get; set; } = new();

    /// <summary>The [tasks.export.linear] table.</summary>
    public TasksExportLinearSection Linear { get; set; } = new();
}
