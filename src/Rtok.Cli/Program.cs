using System.Reflection;
using ConsoleAppFramework;
using Rtok.Cli;
using Rtok.Cli.Shell;
using Rtok.Cli.Ui;
using Rtok.Process;

if (args is ["--version"] or ["-V"])
{
    Console.WriteLine(VersionText());
    return;
}

// The hook stays on its budget: no command model and no display.
var hook = args is ["hook", ..];

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
app.Add<PluginsCommands>();
app.Add<ShellCommands>();
app.Add("hook", HookCommand.Run);
app.Run(args);

// "rtok 0.1.0 (3ebd2f7a1)": the informational version is "<version>+<sha9>" (Directory.Build.targets).
static string VersionText()
{
    var info =
        typeof(Program)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.0.0";
    var plus = info.IndexOf('+', StringComparison.Ordinal);
    return plus < 0 ? $"rtok {info}" : $"rtok {info[..plus]} ({info[(plus + 1)..]})";
}
