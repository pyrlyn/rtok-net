using System.Text.Json;
using Rtok.Config;
using Rtok.Store;
using Rtok.Tasks.Export;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks;

/// <summary>
/// <c>rtok task …</c>. Each command loads the config, opens the checkout's tasks and returns the
/// exit code: 0 on success, 1 with one line on stderr when the request or a service fails.
/// <c>--json</c> prints indented <see cref="TaskJson"/>; text follows <see cref="TaskText"/>.
/// </summary>
/// <param name="stdout">Where results go.</param>
/// <param name="stderr">Where errors and warnings go.</param>
/// <param name="context">The working directory, config options, HTTP, environment and clock.</param>
public sealed class TaskCli(TextWriter stdout, TextWriter stderr, TaskCliContext context)
{
    /// <summary>Writes <c>[tasks]</c> into the checkout's <c>.rtok.toml</c>, then seeds the top-level counter.</summary>
    /// <param name="adapter"><c>disk</c>, <c>github</c> or <c>gitlab</c>; null keeps the file's.</param>
    /// <param name="prefix">The id prefix; null keeps the file's.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> InitAsync(string? adapter, string? prefix, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            var file = TaskInit.Write(context.Cwd, adapter, prefix);
            // Loaded after the write: the file just changed what [tasks] says.
            await WithProjectAsync(
                    async (p, config) =>
                    {
                        var last = await p.SeedAsync(null, ct).ConfigureAwait(false);
                        stdout.WriteLine(
                            $"{file}: adapter {config.Tasks.Adapter}, next id {p.Prefix}{last + 1}"
                        );
                    },
                    ct
                )
                .ConfigureAwait(false);
        });

    /// <summary>Creates a task under the next free id and prints the id.</summary>
    /// <param name="title">The title.</param>
    /// <param name="description">The body.</param>
    /// <param name="bodyFile">A file to read the body from, <c>-</c> for stdin.</param>
    /// <param name="parent">The parent, for a subtask.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> CreateAsync(
        string title,
        string? description,
        string? bodyFile,
        string? parent,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var body = await BodyAsync(description, bodyFile, ct).ConfigureAwait(false);
                var task = await p.CreateAsync(new NewPlanTask(title, body, Id(parent)), ct)
                    .ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, task), task.Id.ToString());
            },
            ct
        );

    /// <summary>Prints the plan as a table: active tasks unless filtered, subtasks under their parent.</summary>
    /// <param name="statuses">Only these states; each item may be comma-separated.</param>
    /// <param name="all">Finished tasks too.</param>
    /// <param name="parent">Only the subtasks of this task.</param>
    /// <param name="json">Print JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ListAsync(
        IReadOnlyList<string> statuses,
        bool all,
        string? parent,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                ArgumentNullException.ThrowIfNull(statuses);
                var filter = new TaskFilter
                {
                    Statuses =
                    [
                        .. statuses
                            .SelectMany(s => s.Split(',', StringSplitOptions.TrimEntries))
                            .Where(s => s.Length > 0)
                            .Select(TaskStates.Parse),
                    ],
                    All = all,
                    Parent = Id(parent),
                };
                var tasks = await p.ListAsync(filter, ct).ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, tasks), TaskText.Table(tasks), line: false);
            },
            ct
        );

    /// <summary>Prints one task with its subtasks.</summary>
    /// <param name="id">The id.</param>
    /// <param name="json">Print JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ShowAsync(string id, bool json, CancellationToken ct = default) =>
        RunAsync(
            async p =>
            {
                var view = await p.GetAsync(TaskId.Parse(id), ct).ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, view), TaskText.Details(view), line: false);
            },
            ct
        );

    /// <summary>Prints a task's state, or sets it.</summary>
    /// <param name="id">The id.</param>
    /// <param name="status">The new state, or null to read.</param>
    /// <param name="force">Finish a parent with open subtasks.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> StatusAsync(
        string id,
        string? status,
        bool force,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var state = status is null ? (TaskState?)null : TaskStates.Parse(status);
                var task = await p.StatusAsync(TaskId.Parse(id), state, force, ct)
                    .ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, task), $"{task.Id}: {task.Status.Wire()}");
            },
            ct
        );

    /// <summary>Prints the best ready task, or <c>no ready task</c>.</summary>
    /// <param name="json">Print the task as JSON, <c>null</c> when none.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> NextAsync(bool json, CancellationToken ct = default) =>
        RunAsync(
            async p =>
            {
                var next = await p.NextAsync(ct).ConfigureAwait(false);
                Print(
                    json,
                    w => Nullable(w, next),
                    next is null ? "no ready task" : $"{next.Id}  {next.Title}"
                );
            },
            ct
        );

    /// <summary>Prints the ready queue, best first.</summary>
    /// <param name="json">Print JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ReadyAsync(bool json, CancellationToken ct = default) =>
        RunAsync(
            async p =>
            {
                var ready = await p.ReadyAsync(ct).ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, ready), TaskText.Ready(ready), line: false);
            },
            ct
        );

    /// <summary>Claims a task, or the first ready one, for the calling agent.</summary>
    /// <param name="id">The task, or null for the first ready one.</param>
    /// <param name="agent">The agent id; <c>RTOK_AGENT_ID</c> when null.</param>
    /// <param name="json">Print the task as JSON, with <c>changed</c>.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ClaimAsync(
        string? id,
        string? agent,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var caller = await AgentAsync(p, agent, ct).ConfigureAwait(false);
                var claim = await p.ClaimAsync(Id(id), caller, ct).ConfigureAwait(false);
                var mine = claim.Changed ? "" : "  (already yours)";
                Print(
                    json,
                    w => TaskJson.Write(w, claim),
                    $"{claim.Task.Id}  {claim.Task.Title}{mine}"
                );
            },
            ct
        );

    /// <summary>Gives a claimed task back.</summary>
    /// <param name="id">The task.</param>
    /// <param name="agent">The agent id; <c>RTOK_AGENT_ID</c> when null.</param>
    /// <param name="force">Release another agent's claim.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ReleaseAsync(
        string id,
        string? agent,
        bool force,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var caller = await AgentAsync(p, agent, ct).ConfigureAwait(false);
                var task = await p.ReleaseAsync(TaskId.Parse(id), caller, force, ct)
                    .ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, task), $"{task.Id}: {task.Status.Wire()}");
            },
            ct
        );

    /// <summary>Makes a task wait for another.</summary>
    /// <param name="id">The task that waits.</param>
    /// <param name="blocker">What it waits for.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> DepAsync(
        string id,
        string blocker,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var task = await p.BlockAsync(TaskId.Parse(id), TaskId.Parse(blocker), ct)
                    .ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, task), $"{id}: blocked by {blocker}");
            },
            ct
        );

    /// <summary>Marks two tasks as not to run at once, or takes the mark off.</summary>
    /// <param name="id">One task.</param>
    /// <param name="other">The other.</param>
    /// <param name="remove">Take the mark off.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ConflictAsync(
        string id,
        string other,
        bool remove,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var task = await p.ConflictAsync(TaskId.Parse(id), TaskId.Parse(other), remove, ct)
                    .ConfigureAwait(false);
                var verb = remove ? "no longer conflicts with" : "conflicts with";
                Print(json, w => TaskJson.Write(w, task), $"{id}: {verb} {other}");
            },
            ct
        );

    /// <summary>Marks a task as safe beside any other, or takes the mark off.</summary>
    /// <param name="id">The task.</param>
    /// <param name="off">Take the mark off.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ParallelAsync(
        string id,
        bool off,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var task = await p.SetParallelAsync(TaskId.Parse(id), !off, ct)
                    .ConfigureAwait(false);
                var word = off ? "not parallel" : "parallel";
                Print(json, w => TaskJson.Write(w, task), $"{task.Id}: {word}");
            },
            ct
        );

    /// <summary>Sets a task's priority.</summary>
    /// <param name="id">The task.</param>
    /// <param name="level">0 (highest) to 4.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> PriorityAsync(
        string id,
        long level,
        bool json,
        CancellationToken ct = default
    ) =>
        RunAsync(
            async p =>
            {
                var task = await p.SetPriorityAsync(TaskId.Parse(id), level, ct)
                    .ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, task), $"{task.Id}: priority {level}");
            },
            ct
        );

    /// <summary>Raises the counters to the adapter's highest ids and reports drift.</summary>
    /// <param name="json">Print JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> SyncAsync(bool json, CancellationToken ct = default) =>
        RunAsync(
            async p =>
            {
                var report = await p.SyncAsync(ct).ConfigureAwait(false);
                Print(json, w => TaskJson.Write(w, report), TaskText.Sync(report), line: false);
            },
            ct
        );

    /// <summary>Mirrors the disk tasks into an issue tracker.</summary>
    /// <param name="backend"><c>github</c> or <c>linear</c>.</param>
    /// <param name="dryRun">List the writes and send none.</param>
    /// <param name="json">Print JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code.</returns>
    public Task<int> ExportAsync(
        string backend,
        bool dryRun,
        bool json,
        CancellationToken ct = default
    ) =>
        GuardAsync(async () =>
        {
            var config = Load();
            TaskExport.RequireDisk(config.Tasks.Adapter);
            var root = TaskProjects.Root(context.Cwd);
            var key = ProjectKey.Of(root);
            var remote = Remote();
            var exporter = await TaskExport
                .OpenAsync(
                    config.Tasks,
                    key,
                    backend,
                    remote,
                    context.Endpoints.GitHub,
                    context.Endpoints.Linear,
                    ct
                )
                .ConfigureAwait(false);
            var disk = await TaskProjects
                .OpenAdapterAsync(config.Tasks, root, key, remote, context.Endpoints, ct)
                .ConfigureAwait(false);
            var local = await disk.ListAsync(new TaskFilter { All = true }, ct)
                .ConfigureAwait(false);
            var report = await TaskExport
                .RunAsync(exporter, local, dryRun, ct)
                .ConfigureAwait(false);
            Print(json, w => TaskJson.Write(w, report), TaskText.Export(report), line: false);
        });

    private Task<int> RunAsync(Func<TaskProject, Task> action, CancellationToken ct) =>
        GuardAsync(() => WithProjectAsync((p, _) => action(p), ct));

    private async Task WithProjectAsync(
        Func<TaskProject, RtokConfig, Task> action,
        CancellationToken ct
    )
    {
        var config = Load();
        var root = TaskProjects.Root(context.Cwd);
        var key = ProjectKey.Of(root);
        var prefix = TaskProjects.Prefix(config.Tasks.Prefix, ProjectKey.Name(root));
        var adapter = await TaskProjects
            .OpenAdapterAsync(config.Tasks, root, key, Remote(), context.Endpoints, ct)
            .ConfigureAwait(false);
        using var store = RtokStore.Open(
            new StoreOptions(config.Core.DbPath) { Time = context.Clock }
        );
        var project = new TaskProject(
            adapter,
            new StoreTaskStore(store),
            key,
            prefix,
            context.Clock
        );
        await action(project, config).ConfigureAwait(false);
    }

    private async Task<int> GuardAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return 0;
        }
        catch (Exception e) when (e is TaskException or ConfigException or StoreRefusedException)
        {
            stderr.WriteLine(e.Message);
            return 1;
        }
    }

    private RtokConfig Load()
    {
        var loaded = ConfigLoader.Load(context.Options with { WorkingDirectory = context.Cwd });
        foreach (var issue in loaded.Issues)
        {
            stderr.WriteLine($"warning: {issue} (using the default)");
        }
        return loaded.Config;
    }

    private RemoteContext Remote() =>
        new(context.Http, context.Env, context.Run, context.Clock, stderr.WriteLine)
        {
            UserAgent = context.UserAgent,
            WriteGap = context.WriteGap,
        };

    private Task<string> AgentAsync(TaskProject project, string? flag, CancellationToken ct) =>
        project.RequireAgentAsync(flag, context.Env("RTOK_AGENT_ID"), stderr.WriteLine, ct);

    private async Task<string> BodyAsync(
        string? description,
        string? bodyFile,
        CancellationToken ct
    )
    {
        if (bodyFile is null)
        {
            return description ?? "";
        }
        if (description is not null)
        {
            throw new TaskException("--description and --body-file cannot be used together");
        }
        if (string.Equals(bodyFile, "-", StringComparison.Ordinal))
        {
            return await context.Stdin.ReadToEndAsync(ct).ConfigureAwait(false);
        }
        var path = Path.GetFullPath(bodyFile, context.Cwd);
        try
        {
            return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new TaskException($"{path}: {e.Message}", e);
        }
    }

    private static TaskId? Id(string? text) => text is null ? null : TaskId.Parse(text);

    private static void Nullable(Utf8JsonWriter writer, PlanTask? task)
    {
        if (task is null)
        {
            writer.WriteNullValue();
            return;
        }
        TaskJson.Write(writer, task);
    }

    /// <summary>JSON, or the text: one line, or a block that already ends with its newline.</summary>
    private void Print(bool json, Action<Utf8JsonWriter> write, string text, bool line = true)
    {
        if (json)
        {
            stdout.WriteLine(TaskJson.Render(write, indented: true));
        }
        else if (line)
        {
            stdout.WriteLine(text);
        }
        else
        {
            stdout.Write(text);
        }
    }
}
