namespace Rtok.Config;

/// <summary>one Markdown file per task</summary>
/// <remarks>Read from [tasks.disk].</remarks>
public sealed class TasksDiskSection
{
    /// <summary>one Markdown file per task, relative to the project root; done ones go to &lt;dir&gt;/done</summary>
    public string Dir { get; set; } = "tasks";
}
