using System.Net.Http.Headers;
using System.Text.Json;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.GitHub;

/// <summary>
/// Tasks as GitHub issues: labelled <c>rtok</c> and <c>rtok:&lt;ID&gt;</c>, titled
/// <c>R2.1. Title</c>, subtasks linked as sub-issues, claims and markers as labels, and the
/// state optionally mirrored into a Projects v2 board.
/// </summary>
public sealed class GitHubTaskAdapter : ITaskAdapter
{
    /// <summary>The public API.</summary>
    public const string DefaultBase = "https://api.github.com";

    private const string Adapter = "github";

    private readonly RemoteClient _client;
    private readonly GitHubProject _project;
    private readonly Action<string> _warn;

    internal GitHubTaskAdapter(RemoteClient client, string repo, long project, Action<string> warn)
    {
        _client = client;
        Repo = repo;
        _warn = warn;
        _project = new GitHubProject(
            client,
            repo[..repo.IndexOf('/', StringComparison.Ordinal)],
            project,
            warn
        );
    }

    /// <inheritdoc/>
    public string Name => Adapter;

    /// <inheritdoc/>
    public bool StoresMarkers => true;

    /// <summary><c>owner/name</c>.</summary>
    public string Repo { get; }

    private string Issues => $"{_client.Base}/repos/{Repo}/issues";

    /// <summary>Opens the adapter with a token from the environment or <c>gh auth token</c>.</summary>
    /// <param name="repo">The checked <c>owner/name</c> (<see cref="ResolveRepo"/>).</param>
    /// <param name="project">The Projects v2 number under the owner; 0 is none.</param>
    /// <param name="context">HTTP, environment and clock.</param>
    /// <param name="apiBase">The API base.</param>
    /// <param name="ct">Cancels the token helper.</param>
    /// <returns>The adapter.</returns>
    /// <exception cref="TaskException">No token.</exception>
    public static async Task<GitHubTaskAdapter> OpenAsync(
        string repo,
        long project,
        RemoteContext context,
        string apiBase = DefaultBase,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        var token = await context
            .TokenAsync(Adapter, ["GH_TOKEN", "GITHUB_TOKEN"], ["gh", "auth", "token"], ct)
            .ConfigureAwait(false);
        var client = new RemoteClient(
            context.Http,
            Adapter,
            apiBase,
            request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                request.Headers.UserAgent.ParseAdd(context.UserAgent);
            },
            context.Clock,
            context.WriteGap
        );
        return new GitHubTaskAdapter(client, repo, project, context.Warn);
    }

    /// <summary>The repository: <c>[tasks.github] repo</c>, else the origin on github.com.</summary>
    /// <param name="configured">The configured value, possibly empty.</param>
    /// <param name="projectKey">The project key (<c>github.com/owner/name</c>).</param>
    /// <returns><c>owner/name</c>.</returns>
    /// <exception cref="TaskException">Neither names a GitHub repository.</exception>
    public static string ResolveRepo(string configured, string projectKey)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(projectKey);
        var repo = configured.Trim();
        if (repo.Length == 0)
        {
            const string Host = "github.com/";
            if (!projectKey.StartsWith(Host, StringComparison.Ordinal))
            {
                throw new TaskException(
                    $"github tasks: origin {projectKey} is not on github.com; set [tasks.github] repo"
                );
            }
            repo = projectKey[Host.Length..];
        }
        var parts = repo.Split('/');
        if (parts.Length != 2 || !parts.All(IsSegment))
        {
            throw new TaskException($"github tasks: repo \"{repo}\" is not owner/name");
        }
        return repo;
    }

    internal static bool IsSegment(string s) =>
        s.Length > 0
        && s is not ("." or "..")
        && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    /// <inheritdoc/>
    public async Task<PlanTask> CreateAsync(
        TaskId id,
        NewPlanTask task,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(task.Title))
        {
            throw new TaskException("github tasks: a task needs a title");
        }
        if (await FindIssueAsync(id, ct).ConfigureAwait(false) is not null)
        {
            throw new TaskTakenException($"github tasks: {Repo}: task {id} already exists");
        }
        GitHubIssue? parent = null;
        if (id.Parent is { } parentId)
        {
            parent =
                await FindIssueAsync(parentId, ct).ConfigureAwait(false)
                ?? throw new TaskException($"github tasks: no parent task {parentId} in {Repo}");
        }
        var created = await CreateIssueAsync(id, task.Title, task.Description, ct)
            .ConfigureAwait(false);
        if (parent is not null)
        {
            await LinkAsync(id, parent, created, ct).ConfigureAwait(false);
        }
        await _project.SyncAsync(id, created.NodeId, TaskState.Open, ct).ConfigureAwait(false);
        return Held(id, created);
    }

    /// <inheritdoc/>
    public async Task<PlanTask?> FindAsync(TaskId id, CancellationToken ct = default) =>
        (await FindIssueAsync(id, ct).ConfigureAwait(false))?.ToTask();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlanTask>> ListAsync(
        TaskFilter filter,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(filter);
        var issues = await ListIssuesAsync(RemoteLabels.Rtok, filter.WantsFinished, ct)
            .ConfigureAwait(false);
        return filter.Select(issues.Select(i => i.ToTask()).OfType<PlanTask>());
    }

    /// <inheritdoc/>
    public async Task<PlanTask> WriteStatusAsync(
        TaskId id,
        TaskState state,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        var issue = await RequireAsync(id, ct).ConfigureAwait(false);
        List<string> labels = [.. issue.Labels.Where(NotInProgress)];
        return await PatchAsync(id, issue, labels, state, null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PlanTask> SaveAsync(PlanTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        var issue = await RequireAsync(task.Id, ct).ConfigureAwait(false);
        return await PatchAsync(task.Id, issue, Labels(issue, task), task.Status, null, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IDisposable> LockClaimsAsync(CancellationToken ct = default) => NoLock.Task;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StrayIssue>> UnlabelledAsync(CancellationToken ct = default)
    {
        var issues = await ListIssuesAsync(RemoteLabels.Rtok, finished: true, ct)
            .ConfigureAwait(false);
        return
        [
            .. issues
                .Where(i => i.Id is null)
                .Select(i => new StrayIssue(i.Title, i.Url, RemoteLabels.TitleId(i.Title))),
        ];
    }

    /// <summary>Every issue labelled <c>rtok</c> that names a task, in any state; for export.</summary>
    internal async Task<IReadOnlyList<GitHubIssue>> AllIssuesAsync(CancellationToken ct) =>
        [
            .. (
                await ListIssuesAsync(RemoteLabels.Rtok, finished: true, ct).ConfigureAwait(false)
            ).Where(i => i.Id is not null),
        ];

    /// <summary>Opens an issue with the id label and no other; for export too.</summary>
    internal async Task<GitHubIssue> CreateIssueAsync(
        TaskId id,
        string title,
        string description,
        CancellationToken ct
    )
    {
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                w.WriteString("title", RemoteLabels.Title(id, title));
                w.WriteString("body", description);
                w.WriteStartArray("labels");
                w.WriteStringValue(RemoteLabels.Rtok);
                w.WriteStringValue(RemoteLabels.IdLabel(id));
                w.WriteEndArray();
                w.WriteEndObject();
            },
            indented: false
        );
        var created = GitHubIssue.Parse(
            await _client.SendAsync(HttpMethod.Post, Issues, body, ct).ConfigureAwait(false)
        );
        if (created.Id != id)
        {
            throw new TaskException(
                $"github tasks: {created.Url} was created without the {RemoteLabels.IdLabel(id)} label (labels need push access to {Repo}); label or close it by hand"
            );
        }
        return created;
    }

    /// <summary>Writes labels and state, and with <paramref name="text"/> the title and body too.</summary>
    internal async Task<PlanTask> PatchAsync(
        TaskId id,
        GitHubIssue issue,
        List<string> labels,
        TaskState state,
        PlanTask? text,
        CancellationToken ct
    )
    {
        if (state == TaskState.InProgress)
        {
            labels.Add(RemoteLabels.InProgress);
        }
        var reason = state switch
        {
            TaskState.Done => "completed",
            TaskState.Closed => "not_planned",
            _ when issue.Closed => "reopened",
            _ => null,
        };
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                if (text is not null)
                {
                    w.WriteString("title", RemoteLabels.Title(id, text.Title));
                    w.WriteString("body", text.Description);
                }
                w.WriteStartArray("labels");
                foreach (var label in labels)
                {
                    w.WriteStringValue(label);
                }
                w.WriteEndArray();
                w.WriteString("state", state.IsActive() ? "open" : "closed");
                if (reason is not null)
                {
                    w.WriteString("state_reason", reason);
                }
                w.WriteEndObject();
            },
            indented: false
        );
        var patched = GitHubIssue.Parse(
            await _client
                .SendAsync(HttpMethod.Patch, $"{Issues}/{issue.Number}", body, ct)
                .ConfigureAwait(false)
        );
        var task = Held(id, patched);
        await _project.SyncAsync(id, patched.NodeId, task.Status, ct).ConfigureAwait(false);
        return task;
    }

    /// <summary>Makes the task's issue match it: title, body, labels and state; for export.</summary>
    internal async Task<PlanTask> MirrorAsync(PlanTask task, CancellationToken ct)
    {
        var issue = await RequireAsync(task.Id, ct).ConfigureAwait(false);
        return await PatchAsync(task.Id, issue, Labels(issue, task), task.Status, task, ct)
            .ConfigureAwait(false);
    }

    /// <summary>The labels a save writes: the issue's own, then the task's managed ones.</summary>
    internal static List<string> Labels(GitHubIssue issue, PlanTask task) =>
        RemoteLabels.Merge(issue.Labels.Where(NotInProgress), task, Adapter);

    private static bool NotInProgress(string label) =>
        !string.Equals(label, RemoteLabels.InProgress, StringComparison.OrdinalIgnoreCase);

    private async Task LinkAsync(
        TaskId id,
        GitHubIssue parent,
        GitHubIssue child,
        CancellationToken ct
    )
    {
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                w.WriteNumber("sub_issue_id", child.DatabaseId);
                w.WriteEndObject();
            },
            indented: false
        );
        try
        {
            await _client
                .SendAsync(HttpMethod.Post, $"{Issues}/{parent.Number}/sub_issues", body, ct)
                .ConfigureAwait(false);
        }
        catch (TaskException e)
        {
            _warn($"github tasks: {id} is not linked as a sub-issue of {parent.Id}: {e.Message}");
        }
    }

    private static PlanTask Held(TaskId id, GitHubIssue issue) =>
        issue.Id == id
            ? issue.ToTask()!
            : throw new TaskException($"github tasks: {id} lost its label");

    private async Task<GitHubIssue> RequireAsync(TaskId id, CancellationToken ct) =>
        await FindIssueAsync(id, ct).ConfigureAwait(false)
        ?? throw new TaskException($"github tasks: no task {id} in {Repo}");

    private async Task<GitHubIssue?> FindIssueAsync(TaskId id, CancellationToken ct) =>
        (
            await ListIssuesAsync(RemoteLabels.IdLabel(id), finished: true, ct)
                .ConfigureAwait(false)
        ).FirstOrDefault(i => i.Id == id);

    private async Task<List<GitHubIssue>> ListIssuesAsync(
        string label,
        bool finished,
        CancellationToken ct
    )
    {
        var url =
            $"{Issues}?labels={Uri.EscapeDataString(label)}&state={(finished ? "all" : "open")}&per_page=100";
        var items = await _client.GetAllAsync(url, ct).ConfigureAwait(false);
        return [.. items.Select(GitHubIssue.Parse).Where(i => !i.IsPull)];
    }
}
