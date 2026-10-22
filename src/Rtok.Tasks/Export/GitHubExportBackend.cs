using Rtok.Tasks.GitHub;

namespace Rtok.Tasks.Export;

/// <summary>
/// The GitHub Issues mirror. It drives the <c>github</c> task adapter, so labels, titles,
/// sub-issue links and close reasons are what that adapter writes and reads: an exported
/// repository can later become the project's task store.
/// </summary>
internal sealed class GitHubExportBackend(GitHubTaskAdapter adapter) : IExportBackend
{
    public string Name => "github";

    public string Target => adapter.Repo;

    public bool KeepsClaims => true;

    public Task<IReadOnlyList<PlanTask>> RemoteAsync(CancellationToken ct = default) =>
        adapter.ListAsync(new TaskFilter { All = true }, ct);

    public async Task<string?> ApplyAsync(
        ExportAction action,
        PlanTask task,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(task);
        PlanTask issue;
        if (action.Kind == ExportKind.Create)
        {
            var created = await adapter
                .CreateAsync(
                    task.Id,
                    new NewPlanTask(task.Title, task.Description, task.Parent),
                    ct
                )
                .ConfigureAwait(false);
            // Create opens a bare issue; labels and a finished state need one more write.
            issue =
                TaskExport.Drifted(task, created, claims: true).Count == 0
                    ? created
                    : await adapter.MirrorAsync(task, ct).ConfigureAwait(false);
        }
        else
        {
            issue = await adapter.MirrorAsync(task, ct).ConfigureAwait(false);
        }
        return issue.External?.Url;
    }
}
