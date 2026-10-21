namespace Rtok.Config;

/// <summary>GitLab Issues with `status::` labels</summary>
/// <remarks>Read from [tasks.gitlab].</remarks>
public sealed class TasksGitlabSection
{
    /// <summary>https base URL; set it for a self-hosted instance</summary>
    public string Url { get; set; } = "https://gitlab.com";

    /// <summary>group/name or numeric id; empty: the origin remote</summary>
    public string Project { get; set; } = "";
}
