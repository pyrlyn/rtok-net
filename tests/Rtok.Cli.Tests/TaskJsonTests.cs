using System.Globalization;
using System.Text;
using Rtok.Config;
using Rtok.Tasks;
using Rtok.Tasks.Tests;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary>
/// <c>rtok task … --json</c> over a temp disk store, through the <see cref="TaskCli"/> the binary
/// calls. The test host runs on the JIT: a Release build of the binary is AOT-shaped and cannot
/// build the store's model, which only an AOT publish compiles in.
/// </summary>
public sealed class TaskJsonTests : IDisposable
{
    private readonly TempDir _temp = new("rtok-task-json-");
    private readonly HttpClient _http = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public TaskJsonTests()
    {
        GitRepo.Init(_temp.Combine("app"), "https://github.com/me/app.git");
        var db = _temp.Combine("rtok.db").Replace('\\', '/');
        _temp.Write("config.toml", $"[core]\ndb_path = \"{db}\"\n");
    }

    public void Dispose()
    {
        _temp.Dispose();
        _http.Dispose();
        _out.Dispose();
        _err.Dispose();
    }

    [Fact]
    public async Task Init_is_read_by_the_next_call_and_every_command_prints_its_json()
    {
        var log = new StringBuilder();
        async Task StepAsync(string line, Func<TaskCli, CancellationToken, Task<int>> command)
        {
            var code = await command(Cli(), TestContext.Current.CancellationToken);
            log.Append(
                CultureInfo.InvariantCulture,
                $"$ rtok task {line}\n{_out}{_err}(exit {code})\n"
            );
            _out.GetStringBuilder().Clear();
            _err.GetStringBuilder().Clear();
        }

        await StepAsync("init --adapter disk --prefix a", (c, ct) => c.InitAsync("disk", "a", ct));
        await StepAsync(
            "create Ship -d Why. --json",
            (c, ct) => c.CreateAsync("Ship", "Why.", null, null, true, ct)
        );
        await StepAsync(
            "create Sub --parent A1 --json",
            (c, ct) => c.CreateAsync("Sub", null, null, "A1", true, ct)
        );
        await StepAsync(
            "create Two --json",
            (c, ct) => c.CreateAsync("Two", null, null, null, true, ct)
        );
        await StepAsync(
            "conflict A2 A1.1 --json",
            (c, ct) => c.ConflictAsync("A2", "A1.1", false, true, ct)
        );
        await StepAsync("parallel A1 --json", (c, ct) => c.ParallelAsync("A1", false, true, ct));
        await StepAsync("list --json", (c, ct) => c.ListAsync([], false, null, true, ct));
        await StepAsync("show A1 --json", (c, ct) => c.ShowAsync("A1", true, ct));
        await StepAsync("next --json", (c, ct) => c.NextAsync(true, ct));
        await StepAsync("ready --json", (c, ct) => c.ReadyAsync(true, ct));
        await StepAsync(
            "status A1.1 done --json",
            (c, ct) => c.StatusAsync("A1.1", "done", false, true, ct)
        );
        await StepAsync("sync --json", (c, ct) => c.SyncAsync(true, ct));

        await Verify(
            log.ToString().ReplaceLineEndings("\n").Replace(_temp.Path, "<temp>").Replace('\\', '/')
        );
    }

    private TaskCli Cli() =>
        new(
            _out,
            _err,
            new TaskCliContext(
                _temp.Combine("app"),
                new ConfigLoadOptions
                {
                    ConfigFile = _temp.Combine("config.toml"),
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["RTOK_HOME"] = _temp.Combine("home"),
                    },
                },
                _http,
                _ => null,
                null,
                new Frozen()
            )
        );

    private sealed class Frozen : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    }
}
