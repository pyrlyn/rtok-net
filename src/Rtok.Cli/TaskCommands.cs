using System.ComponentModel;
using System.Reflection;
using System.Text;
using ConsoleAppFramework;
using Rtok.Config;
using Rtok.Process;
using Rtok.Tasks;

namespace Rtok.Cli;

/// <summary>Plan and track work as tasks in files, GitHub or GitLab.</summary>
internal sealed class TaskCommands
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly TextWriter _stdout = Console.Out;
    private readonly TextWriter _stderr = Console.Error;

    /// <summary>Write [tasks] into this checkout's .rtok.toml and seed the counter from existing tasks.</summary>
    /// <param name="adapter">disk, github or gitlab (default: disk, or what the file already says).</param>
    /// <param name="prefix">Task id prefix, 1–8 ASCII letters (default: the project name's first letter).</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("init")]
    public Task<int> InitAsync(
        string? adapter = null,
        string? prefix = null,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).InitAsync(adapter, prefix, ct);

    /// <summary>Create a task under the next free id and print the id.</summary>
    /// <param name="title">One-line title.</param>
    /// <param name="description">-d, Description: why the task exists and what done means.</param>
    /// <param name="bodyFile">Read the description from this file (- for stdin).</param>
    /// <param name="parent">Make it a subtask of this task, e.g. R2 → R2.1.</param>
    /// <param name="json">Print the task as JSON instead of the id.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("create")]
    public Task<int> CreateAsync(
        [Argument] string title,
        string? description = null,
        string? bodyFile = null,
        string? parent = null,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).CreateAsync(title, description, bodyFile, parent, json, ct);

    /// <summary>The plan: open and in-progress tasks, subtasks under their parent.</summary>
    /// <param name="status">Only these statuses (comma-separated: open, in-progress, done, closed).</param>
    /// <param name="all">Done and closed tasks too.</param>
    /// <param name="parent">Only the subtasks of this task.</param>
    /// <param name="json">JSON instead of the table.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("list")]
    public Task<int> ListAsync(
        string? status = null,
        bool all = false,
        string? parent = null,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ListAsync(status is null ? [] : [status], all, parent, json, ct);

    /// <summary>One task: title, status, parent, subtasks, link and description.</summary>
    /// <param name="id">Task id, e.g. R12 or R2.1.</param>
    /// <param name="json">JSON instead of the text.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("show")]
    public Task<int> ShowAsync(
        [Argument] string id,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ShowAsync(id, json, ct);

    /// <summary>Print a task's status, or set it; done and closed move it out of the plan.</summary>
    /// <param name="id">Task id, e.g. R12 or R2.1.</param>
    /// <param name="status">open, in-progress, done or closed.</param>
    /// <param name="force">Finish a parent even though subtasks are still open.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("status")]
    public Task<int> StatusAsync(
        [Argument] string id,
        [Argument] string? status = null,
        bool force = false,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).StatusAsync(id, status, force, json, ct);

    /// <summary>The first ready task: free or stale, not blocked; a leaf when nothing is blocked.</summary>
    /// <param name="json">JSON instead of the text.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("next")]
    public Task<int> NextAsync(
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).NextAsync(json, ct);

    /// <summary>Tasks that can be claimed, highest priority first.</summary>
    /// <param name="json">JSON instead of the text.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("ready")]
    public Task<int> ReadyAsync(
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ReadyAsync(json, ct);

    /// <summary>Claim a task for this agent, or the first ready task when no id is given. On GitHub and GitLab the assignee write is last-write-wins.</summary>
    /// <param name="id">Task id; omit to take the first ready task.</param>
    /// <param name="agent">The rtok agent id; defaults to RTOK_AGENT_ID.</param>
    /// <param name="json">Print the task as JSON, with changed.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("claim")]
    public Task<int> ClaimAsync(
        [Argument] string? id = null,
        string? agent = null,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ClaimAsync(id, agent, json, ct);

    /// <summary>Clear the assignee and set the task open. Only the holder, unless --force.</summary>
    /// <param name="id">Task id, e.g. R12.</param>
    /// <param name="agent">The rtok agent id; defaults to RTOK_AGENT_ID.</param>
    /// <param name="force">Release a task held by someone else.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("release")]
    public Task<int> ReleaseAsync(
        [Argument] string id,
        string? agent = null,
        bool force = false,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ReleaseAsync(id, agent, force, json, ct);

    /// <summary>Record that this task waits on another. A cycle is refused and nothing is written.</summary>
    /// <param name="id">The task that waits.</param>
    /// <param name="blocker">The task that must finish first.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("dep")]
    public Task<int> DepAsync(
        [Argument] string id,
        [Argument] string blocker,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).DepAsync(id, blocker, json, ct);

    /// <summary>Set priority from 0 (highest) to 4. 2 is the default and is not stored.</summary>
    /// <param name="id">Task id, e.g. R12.</param>
    /// <param name="level">0 to 4.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("priority")]
    public Task<int> PriorityAsync(
        [Argument] string id,
        [Argument] int level,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).PriorityAsync(id, level, json, ct);

    /// <summary>Record that two tasks touch the same files, so they should not run at once. Symmetric; ready flags a task whose partner is in progress.</summary>
    /// <param name="id">One task.</param>
    /// <param name="other">The task it conflicts with.</param>
    /// <param name="remove">Take the conflict off instead.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("conflict")]
    public Task<int> ConflictAsync(
        [Argument] string id,
        [Argument] string other,
        bool remove = false,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ConflictAsync(id, other, remove, json, ct);

    /// <summary>Mark a task safe to run beside any other task.</summary>
    /// <param name="id">Task id, e.g. R12.</param>
    /// <param name="off">Take the mark off instead.</param>
    /// <param name="json">Print the task as JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("parallel")]
    public Task<int> ParallelAsync(
        [Argument] string id,
        bool off = false,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ParallelAsync(id, off, json, ct);

    /// <summary>Raise the id counters to the adapter's highest ids and report drift; never writes the adapter.</summary>
    /// <param name="json">JSON instead of the text.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("sync")]
    public Task<int> SyncAsync(
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).SyncAsync(json, ct);

    /// <summary>Mirror the disk tasks one way into an issue tracker; the files stay the source of truth. A second run with nothing changed sends no write.</summary>
    /// <param name="backend">Where to mirror the tasks: github or linear.</param>
    /// <param name="dryRun">Print the planned creates, updates and closes and send nothing.</param>
    /// <param name="json">JSON instead of the text.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="ct">Cancels the command.</param>
    [Command("export")]
    public Task<int> ExportAsync(
        [Argument] string backend,
        bool dryRun = false,
        bool json = false,
        string? config = null,
        CancellationToken ct = default
    ) => Cli(config).ExportAsync(backend, dryRun, json, ct);

    private TaskCli Cli(string? config) =>
        new(
            _stdout,
            _stderr,
            new TaskCliContext(
                Directory.GetCurrentDirectory(),
                new ConfigLoadOptions { ConfigFile = config },
                Http,
                Environment.GetEnvironmentVariable,
                QuietAsync,
                TimeProvider.System
            )
            {
                Stdin = Console.In,
                UserAgent = UserAgent(),
            }
        );

    /// <summary>A token helper's trimmed stdout; null when it cannot start, fails or times out.</summary>
    private static async Task<string?> QuietAsync(IReadOnlyList<string> argv, CancellationToken ct)
    {
        try
        {
            var result = await ProgressRunner
                .Quiet(argv[0], argv.Skip(1))
                .Output(OutputMode.Capture)
                .Timeout(TimeSpan.FromSeconds(10))
                .RunAsync(ct)
                .ConfigureAwait(false);
            return result.ExitCode == 0 ? Encoding.UTF8.GetString(result.Stdout).Trim() : null;
        }
        catch (Exception e) when (e is Win32Exception or TimeoutException)
        {
            return null;
        }
    }

    /// <summary><c>rtok/&lt;version&gt;</c>, without the commit.</summary>
    private static string UserAgent()
    {
        var info =
            typeof(TaskCommands)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "0.0.0";
        var plus = info.IndexOf('+', StringComparison.Ordinal);
        return $"rtok/{(plus < 0 ? info : info[..plus])}";
    }
}
