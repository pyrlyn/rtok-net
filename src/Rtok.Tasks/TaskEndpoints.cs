using Rtok.Tasks.Export;
using Rtok.Tasks.GitHub;

namespace Rtok.Tasks;

/// <summary>The service URLs the remote adapters and export backends call; tests point them at a mock.</summary>
public sealed record TaskEndpoints
{
    /// <summary>The GitHub REST and GraphQL base.</summary>
    public string GitHub { get; init; } = GitHubTaskAdapter.DefaultBase;

    /// <summary>The GitLab API base, or null for <c>&lt;[tasks.gitlab] url&gt;/api/v4</c>.</summary>
    public string? GitLabApi { get; init; }

    /// <summary>The Linear GraphQL endpoint.</summary>
    public string Linear { get; init; } = LinearExportBackend.DefaultUrl;
}
