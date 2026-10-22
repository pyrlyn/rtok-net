using Rtok.Config;
using Rtok.Tasks.GitHub;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.Export;

/// <summary>
/// <c>rtok task export</c>: a one-way mirror of the disk tasks into an issue tracker. The files
/// stay the source of truth; no issue is ever read back into a task.
/// </summary>
public static class TaskExport
{
    /// <summary>The backends export takes.</summary>
    public static IReadOnlyList<string> Backends { get; } = ["github", "linear"];

    /// <summary>Export reads the disk adapter only.</summary>
    /// <param name="adapter">The configured <c>[tasks] adapter</c>.</param>
    /// <exception cref="TaskException">It is not <c>disk</c>.</exception>
    public static void RequireDisk(string adapter)
    {
        if (!string.Equals(adapter, "disk", StringComparison.Ordinal))
        {
            throw new TaskException(
                $"rtok task export mirrors the disk adapter, but [tasks] adapter is \"{adapter}\""
            );
        }
    }

    /// <summary>Opens a backend with its target and token resolved.</summary>
    /// <param name="config">The <c>[tasks]</c> table.</param>
    /// <param name="projectKey">The project key, which names the GitHub repo when none is set.</param>
    /// <param name="backend"><c>github</c> or <c>linear</c>.</param>
    /// <param name="context">HTTP, environment and clock.</param>
    /// <param name="githubBase">The GitHub API base.</param>
    /// <param name="linearUrl">The Linear GraphQL endpoint.</param>
    /// <param name="ct">Cancels the token helper.</param>
    /// <returns>The backend.</returns>
    /// <exception cref="TaskException">Unknown backend, no target or no token.</exception>
    public static async Task<IExportBackend> OpenAsync(
        TasksSection config,
        string projectKey,
        string backend,
        RemoteContext context,
        string githubBase = GitHubTaskAdapter.DefaultBase,
        string linearUrl = LinearExportBackend.DefaultUrl,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);
        switch (backend)
        {
            case "github":
                var configured =
                    new[] { config.Export.Github.Repo, config.Github.Repo }.FirstOrDefault(r =>
                        r.Length > 0
                    ) ?? "";
                var repo = GitHubTaskAdapter.ResolveRepo(configured, projectKey);
                // No Projects v2 board: the mirror is issues only.
                var adapter = await GitHubTaskAdapter
                    .OpenAsync(repo, 0, context, githubBase, ct)
                    .ConfigureAwait(false);
                return new GitHubExportBackend(adapter);
            case "linear":
                var team = config.Export.Linear.Team.Trim();
                if (team.Length == 0)
                {
                    throw new TaskException(
                        "linear export: set [tasks.export.linear] team to the team's short key"
                    );
                }
                var key = await context
                    .TokenAsync("linear", ["LINEAR_API_KEY"], [], ct)
                    .ConfigureAwait(false);
                return LinearExportBackend.Open(linearUrl, team, key, context);
            default:
                throw new TaskException(
                    $"rtok task export: unknown backend \"{backend}\"; use {string.Join(" or ", Backends)}"
                );
        }
    }

    /// <summary>
    /// Plans against what the tracker holds and, unless <paramref name="dryRun"/>, applies it.
    /// Stops at the first failed write; the ones before it stay done and a re-run picks up there.
    /// </summary>
    /// <param name="backend">The tracker.</param>
    /// <param name="local">The disk tasks, all states.</param>
    /// <param name="dryRun">Plan only.</param>
    /// <param name="ct">Cancels the run between writes.</param>
    /// <returns>The report.</returns>
    public static async Task<ExportReport> RunAsync(
        IExportBackend backend,
        IReadOnlyList<PlanTask> local,
        bool dryRun,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(local);
        IReadOnlyList<PlanTask> remote;
        try
        {
            remote = await backend.RemoteAsync(ct).ConfigureAwait(false);
        }
        catch (TaskException e)
        {
            throw new TaskException(
                $"{backend.Name} export: listing the existing issues: {e.Message}",
                e
            );
        }
        var (actions, unchanged, skipped) = Diff(local, remote, backend.KeepsClaims);
        if (!dryRun)
        {
            var byId = local.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
            for (var done = 0; done < actions.Count; done++)
            {
                var action = actions[done];
                try
                {
                    var url = await backend
                        .ApplyAsync(action, byId[action.Id], ct)
                        .ConfigureAwait(false);
                    action.Url = url ?? action.Url;
                }
                catch (TaskException e)
                {
                    throw new TaskException(
                        $"{backend.Name} export: {action.Kind.Word()} {action.Id} failed after {done} of {actions.Count} writes: {e.Message}",
                        e
                    );
                }
            }
        }
        return new ExportReport(backend.Name, backend.Target, dryRun, actions, unchanged, skipped);
    }

    /// <summary>
    /// The actions that bring the tracker in line with <paramref name="local"/>, in id order so a
    /// parent comes before its subtasks. A finished task with no issue is skipped, unless an
    /// active subtask needs it as a parent; an issue with no local task is left alone.
    /// </summary>
    internal static (List<ExportAction> Actions, int Unchanged, int Skipped) Diff(
        IReadOnlyList<PlanTask> local,
        IReadOnlyList<PlanTask> remote,
        bool claims
    )
    {
        var held = new Dictionary<TaskId, PlanTask>();
        foreach (var t in remote)
        {
            held.TryAdd(t.Id, t);
        }
        var parents = local
            .Where(t => t.Status.IsActive())
            .Select(t => t.Parent)
            .OfType<TaskId>()
            .ToHashSet();
        var actions = new List<ExportAction>();
        var (unchanged, skipped) = (0, 0);
        foreach (var task in local.OrderBy(t => t.Id))
        {
            var active = task.Status.IsActive();
            if (!held.TryGetValue(task.Id, out var have))
            {
                if (active || parents.Contains(task.Id))
                {
                    actions.Add(
                        new ExportAction(task.Id, ExportKind.Create, active ? [] : ["status"], null)
                    );
                }
                else
                {
                    skipped++;
                }
                continue;
            }
            var fields = Drifted(task, have, claims);
            if (fields.Count == 0)
            {
                unchanged++;
                continue;
            }
            var kind = (active, have.Status.IsActive()) switch
            {
                (false, true) => ExportKind.Close,
                (true, false) => ExportKind.Reopen,
                _ => ExportKind.Update,
            };
            actions.Add(new ExportAction(task.Id, kind, fields, have.External?.Url));
        }
        return (actions, unchanged, skipped);
    }

    /// <summary>The fields of <paramref name="remote"/> that no longer match <paramref name="local"/>, in report order.</summary>
    internal static List<string> Drifted(PlanTask local, PlanTask remote, bool claims)
    {
        var fields = new List<string>();
        if (!string.Equals(Text(local.Title), Text(remote.Title), StringComparison.Ordinal))
        {
            fields.Add("title");
        }
        if (
            !string.Equals(
                Text(local.Description),
                Text(remote.Description),
                StringComparison.Ordinal
            )
        )
        {
            fields.Add("body");
        }
        if (local.Status != remote.Status)
        {
            fields.Add("status");
        }
        if (local.Priority != remote.Priority)
        {
            fields.Add("priority");
        }
        if (claims)
        {
            if (!string.Equals(local.Assignee, remote.Assignee, StringComparison.Ordinal))
            {
                fields.Add("assignee");
            }
            if (!local.BlockedBy.SequenceEqual(remote.BlockedBy))
            {
                fields.Add("needs");
            }
            if (!local.ConflictsWith.SequenceEqual(remote.ConflictsWith))
            {
                fields.Add("conflicts");
            }
            if (local.Parallel != remote.Parallel)
            {
                fields.Add("parallel");
            }
        }
        return fields;
    }

    /// <summary>Text compares without the line endings and edge whitespace a web editor adds.</summary>
    private static string Text(string s) =>
        s.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
}
