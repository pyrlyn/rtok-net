using ConsoleAppFramework;
using Rtok.Cli;
using Rtok.Cli.Shell;
using Rtok.Cli.Ui;
using Rtok.Config;
using Rtok.Log;
using Rtok.Process;
using Rtok.Store;

if (args is ["--version"] or ["-V"])
{
    Console.WriteLine(VersionText());
    return;
}

// `agent`, `agents setup` and `agents remove` are spelled out before anything reads the command.
args = AgentsCommands.Aliases(args);

// The hook stays on its budget: no command model, no display and no config load for the log.
var hook = args is ["hook", ..];

using var log = hook ? null : Logger(args);
log?.ForSource("cli").Named("argv").Debug("argv {Argv}", args);
if (log is not null)
{
    // Long-running surfaces (the proxy) log through the same sink.
    Serilog.Log.Logger = log;
}

// ConsoleAppFramework answers an unknown command with the root help and exit 0.
if (!hook && CommandPath.Check(CommandDocs.Model, args) is var (message, help))
{
    ExitCodeFilter.BadArguments(message, help);
    return;
}

// Spinners and bars on a terminal stderr; hooks and MCP turn them off for themselves.
using var display = hook ? null : TerminalDisplay.ForStderr();
if (display is not null)
{
    ProgressDisplay.Install(display);
}

var app = ConsoleApp.Create();
app.UseFilter<ExitCodeFilter>();
app.Add<AgentsCommands>("agents");
app.Add<ConfigCommands>("config");
app.Add<DocsCommands>("docs");
app.Add<TaskCommands>("task");
app.Add<PluginsCommands>();
app.Add("run", RunCommands.RunAsync);
app.Add("filter", RunCommands.Filter);
app.Add<ShellCommands>();
app.Add("doctor", DoctorCommand.Run);
app.Add("logs", LogsCommands.Show);
app.Add("logs export", LogsCommands.Export);
app.Add("logs watch", LogsCommands.Watch);
app.Add("stats", StatsCommands.RunAsync);
app.Add("bench", BenchCommands.RunAsync);
app.Add("hook", HookCommand.Run);
app.Add("expand", ExpandCommand.Run);
app.Add("proxy", ProxyCommands.ProxyAsync);
app.Add("wrap", ProxyCommands.WrapAsync);
app.Run(args);

// "rtok 0.1.0 (3ebd2f7a1)".
static string VersionText() =>
    BuildInfo.Commit is { } sha ? $"rtok {BuildInfo.Version} ({sha})" : $"rtok {BuildInfo.Version}";

// rtok's log, from the config the command will load (`--config` included). The store opens on
// the first row, so a command that logs nothing never touches it.
static Serilog.Core.Logger Logger(string[] args)
{
    var at = Array.IndexOf(args, "--config");
    var file = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    var config = ConfigLoader.Load(new ConfigLoadOptions { ConfigFile = file }).Config;
    return RtokLog.Create(
        config.Log,
        new RtokLogOptions
        {
            OpenStore = () =>
                RtokStore.Open(
                    new StoreOptions(config.Core.DbPath) { ArchiveDir = config.Core.ArchiveDir }
                ),
        }
    );
}
