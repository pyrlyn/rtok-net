using System.Reflection;
using ConsoleAppFramework;
using Rtok.Cli;
using Rtok.Core;

if (args is ["--version"] or ["-V"])
{
    Console.WriteLine(VersionText());
    return;
}

var app = ConsoleApp.Create();
app.Add<ConfigCommands>("config");

// The linked plugins: id, kind, enabled, surfaces. Until T7 loads the config, nothing
// overrides a plugin's default.
app.Add(
    "plugins",
    (bool json = false) =>
    {
        var registry = new PluginRegistry(LinkedPlugins.Create(), _ => null);
        Console.Out.Write(json ? PluginsTable.RenderJson(registry) : PluginsTable.Render(registry));
    }
);
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
