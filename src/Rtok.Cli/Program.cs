using System.Reflection;
using ConsoleAppFramework;
using Rtok.Cli;

if (args is ["--version"] or ["-V"])
{
    Console.WriteLine(VersionText());
    return;
}

var app = ConsoleApp.Create();

// The linked plugins: id, whether on by default, surfaces. T6 adds configuration and --json.
app.Add("plugins", () => Console.Out.Write(PluginsTable.Render(LinkedPlugins.Create())));
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
