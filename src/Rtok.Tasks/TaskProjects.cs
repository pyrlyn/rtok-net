using Rtok.Config;
using Rtok.Tasks.Disk;
using Rtok.Tasks.GitHub;
using Rtok.Tasks.GitLab;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks;

/// <summary>
/// Opens a checkout's tasks from <c>[tasks]</c>: the checkout, the project key, the id prefix
/// and the adapter. The CLI and the MCP tools both open projects here.
/// </summary>
public static class TaskProjects
{
    /// <summary>The checkout <paramref name="cwd"/> is in.</summary>
    /// <param name="cwd">An absolute directory.</param>
    /// <returns>The checkout.</returns>
    /// <exception cref="TaskException">Not inside a git checkout.</exception>
    public static ProjectRoot Root(string cwd) =>
        ProjectRoot.Find(cwd) ?? throw new TaskException("rtok task: not inside a git checkout");

    /// <summary>
    /// The prefix new ids get: <c>[tasks] prefix</c> upper-cased when set, else the first ASCII
    /// letter of the project name (<c>rtok</c> → <c>R</c>).
    /// </summary>
    /// <param name="configured">The configured prefix, possibly empty.</param>
    /// <param name="projectName">The project's short name (<see cref="ProjectKey.Name"/>).</param>
    /// <returns>The prefix.</returns>
    /// <exception cref="TaskException">The configured prefix is bad, or neither gives one.</exception>
    public static string Prefix(string configured, string? projectName)
    {
        ArgumentNullException.ThrowIfNull(configured);
        if (configured.Length > 0)
        {
            return TaskId.CheckPrefix(configured);
        }
        var letter = projectName?.FirstOrDefault(char.IsAsciiLetter) ?? '\0';
        return letter != '\0'
            ? char.ToUpperInvariant(letter).ToString()
            : throw new TaskException(
                "no task prefix: set [tasks] prefix, since the project name has no ASCII letter"
            );
    }

    /// <summary>Opens the configured adapter.</summary>
    /// <param name="config">The <c>[tasks]</c> table.</param>
    /// <param name="root">The checkout; the disk directory is relative to it.</param>
    /// <param name="key">The project key, which names the remote repository when none is set.</param>
    /// <param name="context">HTTP, environment and clock.</param>
    /// <param name="endpoints">The service URLs.</param>
    /// <param name="ct">Cancels the token helper.</param>
    /// <returns>The adapter.</returns>
    /// <exception cref="TaskException">Unknown adapter, no repository or no token.</exception>
    public static async Task<ITaskAdapter> OpenAdapterAsync(
        TasksSection config,
        ProjectRoot root,
        string key,
        RemoteContext context,
        TaskEndpoints endpoints,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(endpoints);
        return config.Adapter switch
        {
            "disk" => new DiskTaskAdapter(
                Path.GetFullPath(Path.Combine(root.Checkout, config.Disk.Dir)),
                context.Clock
            ),
            "github" => await GitHubTaskAdapter
                .OpenAsync(
                    GitHubTaskAdapter.ResolveRepo(config.Github.Repo, key),
                    config.Github.Project,
                    context,
                    endpoints.GitHub,
                    ct
                )
                .ConfigureAwait(false),
            "gitlab" => await GitLabTaskAdapter
                .OpenAsync(
                    config.Gitlab.Url,
                    config.Gitlab.Project,
                    key,
                    context,
                    endpoints.GitLabApi,
                    ct
                )
                .ConfigureAwait(false),
            _ => throw new TaskException(
                $"rtok task: unknown [tasks] adapter \"{config.Adapter}\""
            ),
        };
    }
}
