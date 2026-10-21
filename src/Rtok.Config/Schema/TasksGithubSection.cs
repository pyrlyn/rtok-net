namespace Rtok.Config;

/// <summary>GitHub Issues, sub-issues and a Projects v2 Status field</summary>
/// <remarks>Read from [tasks.github].</remarks>
public sealed class TasksGithubSection
{
    /// <summary>owner/name; empty: the origin remote</summary>
    public string Repo { get; set; } = "";

    /// <summary>repo owner's Projects v2 number: issues join it and its Status follows the task; 0 = issues only</summary>
    public uint Project { get; set; }
}
