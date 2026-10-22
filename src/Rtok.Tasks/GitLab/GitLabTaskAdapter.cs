using System.Net.Http.Headers;
using System.Text.Json;
using Rtok.Tasks.GitHub;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.GitLab;

/// <summary>
/// Tasks as GitLab issues (REST v4): labelled <c>rtok</c> and <c>rtok:&lt;ID&gt;</c>, the state
/// in scoped <c>status::</c> labels, subtasks linked to their parent, claims and markers as labels.
/// </summary>
public sealed class GitLabTaskAdapter : ITaskAdapter
{
    /// <summary>The public instance.</summary>
    public const string DefaultUrl = "https://gitlab.com";

    private const string Adapter = "gitlab";

    private readonly RemoteClient _client;
    private readonly string _issues;
    private readonly Action<string> _warn;

    internal GitLabTaskAdapter(RemoteClient client, GitLabProjectRef project, Action<string> warn)
    {
        _client = client;
        Project = project.Path;
        _issues = $"{client.Base}/projects/{project.Encoded}/issues";
        _warn = warn;
    }

    /// <inheritdoc/>
    public string Name => Adapter;

    /// <inheritdoc/>
    public bool StoresMarkers => true;

    /// <summary>The project as typed in messages: <c>group/name</c> or a numeric id.</summary>
    public string Project { get; }

    /// <summary>Opens the adapter with a token from the environment or <c>glab</c>.</summary>
    /// <param name="url">The instance, as configured: a plain https base URL.</param>
    /// <param name="project">The configured project, or empty for the origin's.</param>
    /// <param name="projectKey">The project key.</param>
    /// <param name="context">HTTP, environment and clock.</param>
    /// <param name="apiBase">The API base, for tests; defaults to <c>&lt;url&gt;/api/v4</c>.</param>
    /// <param name="ct">Cancels the token helper.</param>
    /// <returns>The adapter.</returns>
    /// <exception cref="TaskException">The instance or project is bad, or there is no token.</exception>
    public static async Task<GitLabTaskAdapter> OpenAsync(
        string url,
        string project,
        string projectKey,
        RemoteContext context,
        string? apiBase = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        var instance = Instance(url);
        var resolved = ResolveProject(project, projectKey, instance);
        var token = await context
            .TokenAsync(
                Adapter,
                ["GITLAB_TOKEN", "GITLAB_ACCESS_TOKEN", "GL_TOKEN"],
                ["glab", "config", "get", "token", "--host", instance.Host],
                ct
            )
            .ConfigureAwait(false);
        var client = new RemoteClient(
            context.Http,
            Adapter,
            apiBase ?? $"{instance.GetLeftPart(UriPartial.Path).TrimEnd('/')}/api/v4",
            request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.UserAgent.ParseAdd(context.UserAgent);
            },
            context.Clock,
            context.WriteGap
        );
        return new GitLabTaskAdapter(client, resolved, context.Warn);
    }

    /// <summary>Checks <c>[tasks.gitlab] url</c>.</summary>
    /// <param name="url">The configured URL.</param>
    /// <returns>The instance.</returns>
    /// <exception cref="TaskException">It is not a plain https base URL.</exception>
    public static Uri Instance(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || uri.Host.Length == 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0
        )
        {
            throw new TaskException(
                $"gitlab tasks: [tasks.gitlab] url \"{url}\" must be a plain https:// base URL"
            );
        }
        return uri;
    }

    /// <summary>The project: <c>[tasks.gitlab] project</c>, else the origin's path on the instance.</summary>
    /// <param name="configured">The configured value, possibly empty.</param>
    /// <param name="projectKey">The project key (<c>gitlab.com/group/name</c>).</param>
    /// <param name="instance">The instance.</param>
    /// <returns>The project for URLs and for messages.</returns>
    /// <exception cref="TaskException">Neither names a project on the instance.</exception>
    internal static GitLabProjectRef ResolveProject(
        string configured,
        string projectKey,
        Uri instance
    )
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(projectKey);
        var project = configured.Trim().Trim('/');
        if (project.Length == 0)
        {
            var slash = projectKey.IndexOf('/', StringComparison.Ordinal);
            var host = slash > 0 ? projectKey[..slash] : "";
            var colon = host.LastIndexOf(':');
            host = colon > 0 ? host[..colon] : host;
            if (!string.Equals(host, instance.Host, StringComparison.OrdinalIgnoreCase))
            {
                throw new TaskException(
                    $"gitlab tasks: origin {projectKey} is not on {instance.Host}; set [tasks.gitlab] project"
                );
            }
            project = projectKey[(slash + 1)..].Trim('/');
            var sub = instance.AbsolutePath.Trim('/');
            if (sub.Length > 0 && project.StartsWith(sub + "/", StringComparison.OrdinalIgnoreCase))
            {
                project = project[(sub.Length + 1)..];
            }
        }
        if (project.All(char.IsAsciiDigit))
        {
            return new GitLabProjectRef(project, project);
        }
        var parts = project.Split('/');
        if (parts.Length < 2 || !parts.All(GitHubTaskAdapter.IsSegment))
        {
            throw new TaskException(
                $"gitlab tasks: project \"{project}\" is not group/name or a numeric id"
            );
        }
        return new GitLabProjectRef(project, string.Join("%2F", parts));
    }

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
            throw new TaskException("gitlab tasks: a task needs a title");
        }
        if (await FindIssueAsync(id, ct).ConfigureAwait(false) is not null)
        {
            throw new TaskTakenException($"gitlab tasks: {Project}: task {id} already exists");
        }
        GitLabIssue? parent = null;
        if (id.Parent is { } parentId)
        {
            parent =
                await FindIssueAsync(parentId, ct).ConfigureAwait(false)
                ?? throw new TaskException($"gitlab tasks: no parent task {parentId} in {Project}");
        }
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                w.WriteString("title", RemoteLabels.Title(id, task.Title));
                w.WriteString("description", task.Description);
                w.WriteString("labels", $"{RemoteLabels.Rtok},{RemoteLabels.IdLabel(id)}");
                w.WriteEndObject();
            },
            indented: false
        );
        var created = GitLabIssue.Parse(
            await _client.SendAsync(HttpMethod.Post, _issues, body, ct).ConfigureAwait(false)
        );
        if (created.Id != id)
        {
            throw new TaskException(
                $"gitlab tasks: {created.Url} was created without the {RemoteLabels.IdLabel(id)} label (labels need more than the Guest role in {Project}); label or close it by hand"
            );
        }
        if (parent is not null)
        {
            await LinkAsync(id, parent, created, ct).ConfigureAwait(false);
        }
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
        var target = GitLabIssue.StatusLabel(state);
        var remove = GitLabIssue.StatusLabels.Where(l =>
            !string.Equals(l, target, StringComparison.Ordinal)
        );
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                if (target is not null)
                {
                    w.WriteString("add_labels", target);
                }
                w.WriteString("remove_labels", string.Join(',', remove));
                StateEvent(w, issue, state);
                w.WriteEndObject();
            },
            indented: false
        );
        return await PutAsync(id, issue, body, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PlanTask> SaveAsync(PlanTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        var issue = await RequireAsync(task.Id, ct).ConfigureAwait(false);
        List<string> desired =
        [
            .. issue.Labels.Where(l => !RemoteLabels.IsManaged(l) && !GitLabIssue.IsStatus(l)),
        ];
        if (GitLabIssue.StatusLabel(task.Status) is { } status)
        {
            desired.Add(status);
        }
        desired.AddRange(RemoteLabels.Managed(task, Adapter));
        var remove = issue.Labels.Where(l => !RemoteLabels.Has(desired, l)).ToList();
        var add = desired.Where(l => !RemoteLabels.Has(issue.Labels, l)).ToList();
        var moves = issue.Closed == task.Status.IsActive();
        if (remove.Count == 0 && add.Count == 0 && !moves)
        {
            return Held(task.Id, issue);
        }
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                if (remove.Count > 0)
                {
                    w.WriteString("remove_labels", string.Join(',', remove));
                }
                if (add.Count > 0)
                {
                    w.WriteString("add_labels", string.Join(',', add));
                }
                StateEvent(w, issue, task.Status);
                w.WriteEndObject();
            },
            indented: false
        );
        return await PutAsync(task.Id, issue, body, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IDisposable> LockClaimsAsync(CancellationToken ct = default) => NoLock.Task;

    /// <inheritdoc/>
    public Task<IReadOnlyList<StrayIssue>> UnlabelledAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StrayIssue>>([]);

    private static void StateEvent(Utf8JsonWriter w, GitLabIssue issue, TaskState state)
    {
        if (issue.Closed && state.IsActive())
        {
            w.WriteString("state_event", "reopen");
        }
        else if (!issue.Closed && !state.IsActive())
        {
            w.WriteString("state_event", "close");
        }
    }

    private async Task<PlanTask> PutAsync(
        TaskId id,
        GitLabIssue issue,
        string body,
        CancellationToken ct
    ) =>
        Held(
            id,
            GitLabIssue.Parse(
                await _client
                    .SendAsync(HttpMethod.Put, $"{_issues}/{issue.Iid}", body, ct)
                    .ConfigureAwait(false)
            )
        );

    private async Task LinkAsync(
        TaskId id,
        GitLabIssue parent,
        GitLabIssue child,
        CancellationToken ct
    )
    {
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                w.WriteNumber("target_project_id", child.ProjectId);
                w.WriteNumber("target_issue_iid", child.Iid);
                w.WriteString("link_type", "relates_to");
                w.WriteEndObject();
            },
            indented: false
        );
        try
        {
            await _client
                .SendAsync(HttpMethod.Post, $"{_issues}/{parent.Iid}/links", body, ct)
                .ConfigureAwait(false);
        }
        catch (TaskException e)
        {
            _warn($"gitlab tasks: {id} is not linked to its parent {parent.Id}: {e.Message}");
        }
    }

    private static PlanTask Held(TaskId id, GitLabIssue issue) =>
        issue.Id == id
            ? issue.ToTask()!
            : throw new TaskException($"gitlab tasks: {id} lost its label");

    private async Task<GitLabIssue> RequireAsync(TaskId id, CancellationToken ct) =>
        await FindIssueAsync(id, ct).ConfigureAwait(false)
        ?? throw new TaskException($"gitlab tasks: no task {id} in {Project}");

    private async Task<GitLabIssue?> FindIssueAsync(TaskId id, CancellationToken ct) =>
        (
            await ListIssuesAsync(RemoteLabels.IdLabel(id), finished: true, ct)
                .ConfigureAwait(false)
        ).FirstOrDefault(i => i.Id == id);

    private async Task<List<GitLabIssue>> ListIssuesAsync(
        string label,
        bool finished,
        CancellationToken ct
    )
    {
        var url =
            $"{_issues}?labels={Uri.EscapeDataString(label)}&state={(finished ? "all" : "opened")}&per_page=100";
        var items = await _client.GetAllAsync(url, ct).ConfigureAwait(false);
        return [.. items.Select(GitLabIssue.Parse)];
    }
}
