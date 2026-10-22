using System.Text.Json;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class TaskCliTests : IDisposable
{
    private readonly TempDir _temp = new("rtok-task-cli-");
    private readonly FakeService _fake = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public TaskCliTests()
    {
        GitRepo.Init(_temp.Combine("app"), "https://github.com/me/app.git");
        var db = _temp.Combine("rtok.db").Replace('\\', '/');
        _temp.Write("config.toml", $"[core]\ndb_path = \"{db}\"\n");
    }

    public void Dispose()
    {
        _fake.Dispose();
        _temp.Dispose();
        _out.Dispose();
        _err.Dispose();
    }

    private string App => _temp.Combine("app");

    private TaskCli Cli(string? cwd = null, string stdin = "") =>
        new(
            _out,
            _err,
            new TaskCliContext(
                cwd ?? App,
                new ConfigLoadOptions
                {
                    ConfigFile = _temp.Combine("config.toml"),
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["RTOK_HOME"] = _temp.Combine("home"),
                    },
                },
                _fake.Http,
                n => _fake.Env.GetValueOrDefault(n),
                null,
                _fake.Clock
            )
            {
                Stdin = new StringReader(stdin),
                WriteGap = TimeSpan.Zero,
                Endpoints = new TaskEndpoints { GitHub = _fake.Url },
            }
        );

    /// <summary>Runs one command, checks its exit code and returns stdout and stderr, then clears both.</summary>
    private async Task<(string Out, string Err)> RunAsync(
        Func<TaskCli, Task<int>> command,
        int code = 0
    )
    {
        var exit = await command(Cli());
        var result = (_out.ToString(), _err.ToString());
        _out.GetStringBuilder().Clear();
        _err.GetStringBuilder().Clear();
        Assert.True(code == exit, $"exit {exit}: {result.Item1}{result.Item2}");
        return result;
    }

    private async Task<string> OutAsync(Func<TaskCli, Task<int>> command) =>
        (await RunAsync(command)).Out.ReplaceLineEndings("\n");

    [Fact]
    public async Task A_disk_plan_from_init_to_sync()
    {
        await BuildPlanAsync();

        Assert.Equal(
            "A1      open         Ship  [parallel]\n"
                + "  A1.1  open         Sub  [conflicts: A2]\n"
                + "A2      open         Two  [conflicts: A1.1]\n",
            await OutAsync(c => c.ListAsync([], false, null, false, Ct))
        );
        Assert.Equal("A1  Ship\n", await OutAsync(c => c.NextAsync(false, Ct)));
        Assert.Equal(
            "A1.1: no longer conflicts with A2\n",
            await OutAsync(c => c.ConflictAsync("A1.1", "A2", true, false, Ct))
        );
        Assert.Equal(
            "A1: not parallel\n",
            await OutAsync(c => c.ParallelAsync("A1", true, false, Ct))
        );
        Assert.Equal(
            "A1.1: done\n",
            await OutAsync(c => c.StatusAsync("A1.1", "done", false, false, Ct))
        );
        Assert.Equal(
            "A1: open\n",
            await OutAsync(c => c.StatusAsync("A1", null, false, false, Ct))
        );
        Assert.Equal(
            "A1.1  Sub\nstatus: done\nparent: A1\n\nFrom stdin.\n",
            await OutAsync(c => c.ShowAsync("A1.1", false, Ct))
        );
        Assert.Equal(
            "disk: next id A3, counters in step\n",
            await OutAsync(c => c.SyncAsync(false, Ct))
        );

        await AssertJsonViewsAsync();
    }

    /// <summary>Init, three tasks, a blocker, a priority, a conflict and a parallel mark, each
    /// checked by its one line.</summary>
    private async Task BuildPlanAsync()
    {
        _temp.Write("app/body.md", "From a file.\n");

        Assert.Equal(
            $"{Path.Combine(App, ".rtok.toml")}: adapter disk, next id A1\n",
            await OutAsync(c => c.InitAsync(null, null, Ct))
        );
        Assert.Equal(
            "A1\n",
            await OutAsync(c => c.CreateAsync("Ship", "Why.", null, null, false, Ct))
        );
        Assert.Equal(
            "A1.1\n",
            await OutAsync(c =>
                Cli(stdin: "From stdin.\n").CreateAsync("Sub", null, "-", "A1", false, Ct)
            )
        );
        Assert.Equal(
            "A2\n",
            await OutAsync(c => c.CreateAsync("Two", null, "body.md", null, false, Ct))
        );
        Assert.Equal("A2: blocked by A1\n", await OutAsync(c => c.DepAsync("A2", "A1", false, Ct)));
        Assert.Equal("A2: priority 0\n", await OutAsync(c => c.PriorityAsync("A2", 0, false, Ct)));
        Assert.Equal(
            "A2: conflicts with A1.1\n",
            await OutAsync(c => c.ConflictAsync("A2", "A1.1", false, false, Ct))
        );
        Assert.Equal(
            "A1: parallel\n",
            await OutAsync(c => c.ParallelAsync("A1", false, false, Ct))
        );
    }

    private async Task AssertJsonViewsAsync()
    {
        using var two = JsonDocument.Parse(await OutAsync(c => c.ShowAsync("A2", true, Ct)));
        Assert.Equal("From a file.", two.RootElement.GetProperty("description").GetString());
        Assert.Equal("A1", two.RootElement.GetProperty("blocked_by")[0].GetString());
        using var done = JsonDocument.Parse(
            await OutAsync(c => c.ListAsync(["done,closed"], false, null, true, Ct))
        );
        Assert.Equal(
            "A1.1",
            Assert.Single(done.RootElement.EnumerateArray()).GetProperty("id").GetString()
        );
        Assert.True(File.Exists(_temp.Combine("app/tasks/done/A1.1 - sub.md")));
    }

    [Fact]
    public async Task Init_keeps_a_set_prefix_and_seeds_from_files_already_there()
    {
        _temp.Write(
            "app/tasks/K7 - old.md",
            "---\nid: K7\ntitle: Old\nstatus: open\ncreated_at: 1\nupdated_at: 1\n---\n"
        );

        Assert.Equal(
            $"{Path.Combine(App, ".rtok.toml")}: adapter disk, next id K8\n",
            await OutAsync(c => c.InitAsync("disk", "k", Ct))
        );
        Assert.Equal(
            "K8\n",
            await OutAsync(c => c.CreateAsync("New", null, null, null, false, Ct))
        );
    }

    [Fact]
    public async Task Nothing_ready_reads_as_text_and_as_null()
    {
        await OutAsync(c => c.InitAsync(null, null, Ct));

        Assert.Equal("no ready task\n", await OutAsync(c => c.NextAsync(false, Ct)));
        Assert.Equal("null\n", await OutAsync(c => c.NextAsync(true, Ct)));
        Assert.Equal("no ready task\n", await OutAsync(c => c.ReadyAsync(false, Ct)));
        Assert.Equal("", await OutAsync(c => c.ListAsync([], false, null, false, Ct)));
    }

    [Fact]
    public async Task Errors_are_one_line_on_stderr_and_exit_1()
    {
        await OutAsync(c => c.InitAsync(null, null, Ct));
        await OutAsync(c => c.CreateAsync("One", null, null, null, false, Ct));

        async Task<string> FailsAsync(Func<TaskCli, Task<int>> command)
        {
            var (stdout, stderr) = await RunAsync(command, code: 1);
            Assert.Equal("", stdout);
            return stderr.ReplaceLineEndings("\n");
        }

        Assert.Equal(
            "no task A9\n",
            await FailsAsync(c => c.StatusAsync("A9", null, false, false, Ct))
        );
        Assert.Equal(
            "--description and --body-file cannot be used together\n",
            await FailsAsync(c => c.CreateAsync("x", "d", "-", null, false, Ct))
        );
        Assert.StartsWith(
            "unknown task status \"bogus\"",
            await FailsAsync(c => c.ListAsync(["open,bogus"], false, null, false, Ct)),
            StringComparison.Ordinal
        );
        Assert.Equal(
            "no agent to bind: pass --agent or set RTOK_AGENT_ID\n",
            await FailsAsync(c => c.ClaimAsync("A1", null, false, Ct))
        );
        Assert.Equal(
            "--agent zz: unknown\n",
            await FailsAsync(c => c.ClaimAsync(null, "zz", false, Ct))
        );
        _fake.Env["RTOK_AGENT_ID"] = "ghost";
        Assert.Equal(
            "warning: RTOK_AGENT_ID ghost is not a known agent; ignored\n"
                + "no agent to bind: pass --agent or set RTOK_AGENT_ID\n",
            await FailsAsync(c => c.ReleaseAsync("A1", null, false, false, Ct))
        );
        Assert.Equal(
            "rtok task: not inside a git checkout\n",
            (
                await RunInAsync(_temp.Path, c => c.ListAsync([], false, null, false, Ct), code: 1)
            ).Err
        );
    }

    [Fact]
    public async Task A_github_plan_is_read_through_the_api()
    {
        _temp.Write("app/.rtok.toml", "[tasks]\nadapter = \"github\"\n");
        _fake.On(
            "GET",
            "/repos/me/app/issues",
            $"[{GitHubJson.Issue(4, "A1. Remote", ["rtok", "rtok:A1"])}]",
            query: new Dictionary<string, string>(StringComparer.Ordinal) { ["labels"] = "rtok" }
        );

        Assert.Equal(
            "A1  open         Remote\n",
            await OutAsync(c => c.ListAsync([], false, null, false, Ct))
        );
        _fake.Env.Remove("GH_TOKEN");
        Assert.Equal(
            "github: no token; set GH_TOKEN or GITHUB_TOKEN or run `gh auth token`\n",
            (
                await RunAsync(c => c.ListAsync([], false, null, false, Ct), code: 1)
            ).Err.ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public async Task Export_plans_from_the_disk_tasks_and_refuses_a_remote_adapter()
    {
        await OutAsync(c => c.InitAsync(null, null, Ct));
        await OutAsync(c => c.CreateAsync("Ship", "Why.", null, null, false, Ct));
        _fake.On("GET", "/repos/me/app/issues", "[]");

        Assert.Equal(
            "github (me/app): 1 create, 0 update, 0 close, 0 reopen, 0 unchanged (dry run: nothing sent)\n"
                + "create A1\n",
            await OutAsync(c => c.ExportAsync("github", true, false, Ct))
        );
        Assert.DoesNotContain(
            _fake.Calls,
            c => !string.Equals(c.Method, "GET", StringComparison.Ordinal)
        );

        _temp.Write("app/.rtok.toml", "[tasks]\nadapter = \"github\"\n");
        Assert.Equal(
            "rtok task export mirrors the disk adapter, but [tasks] adapter is \"github\"\n",
            (
                await RunAsync(c => c.ExportAsync("github", true, false, Ct), code: 1)
            ).Err.ReplaceLineEndings("\n")
        );
    }

    private async Task<(string Out, string Err)> RunInAsync(
        string cwd,
        Func<TaskCli, Task<int>> command,
        int code
    )
    {
        var exit = await command(Cli(cwd));
        var result = (_out.ToString(), _err.ToString().ReplaceLineEndings("\n"));
        _out.GetStringBuilder().Clear();
        _err.GetStringBuilder().Clear();
        Assert.Equal(code, exit);
        return result;
    }
}
