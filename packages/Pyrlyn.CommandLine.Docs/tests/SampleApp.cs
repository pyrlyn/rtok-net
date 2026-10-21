using System.Diagnostics;
using System.Runtime.CompilerServices;
using ConsoleAppFramework;

namespace Pyrlyn.CommandLine.Docs.Tests;

/// <summary>
/// The test assembly doubles as a ConsoleAppFramework program: started with
/// <see cref="Variable"/> set, it runs the sample app on its arguments from the module
/// initializer and exits before the test host runs.
/// </summary>
internal static class SampleApp
{
    internal const string Name = "sample";
    private const string Variable = "PYRLYN_CLDOCS_SAMPLE";

    [ModuleInitializer]
    internal static void RunWhenAsked()
    {
        if (Environment.GetEnvironmentVariable(Variable) is null)
        {
            return;
        }
        var app = ConsoleApp.Create();
        app.ConfigureGlobalOptions(
            (ref ConsoleApp.GlobalOptionsBuilder builder) =>
                builder.AddGlobalOption<bool>("-v|--verbose", "Log more.")
        );
        app.Add<SampleCommands>();
        app.Add<StatsCommand>();
        var args = Environment.GetCommandLineArgs();
        app.Run(args[(Array.IndexOf(args, "--") + 1)..]);
        Environment.Exit(0);
    }

    /// <summary>Runs the sample with these arguments and returns what it printed.</summary>
    internal static string Run(IReadOnlyList<string> args)
    {
        var host = Environment.ProcessPath!;
        var start = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        if (
            string.Equals(
                Path.GetFileNameWithoutExtension(host),
                "dotnet",
                StringComparison.Ordinal
            )
        )
        {
            start.ArgumentList.Add(typeof(SampleApp).Assembly.Location);
        }
        // The test host's own arguments, if any, end before "--".
        start.ArgumentList.Add("--");
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }
        start.Environment[Variable] = "1";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    /// <summary>The model of the sample, read from its help.</summary>
    internal static CommandModel Read() => CommandModelReader.Read(Name, Run);

    /// <summary>
    /// The model of the sample as if one option's doc comment had changed: the generator allows
    /// one <c>stats</c> command per assembly, so the edit is made to the printed help instead.
    /// </summary>
    internal static CommandModel ReadEdited() =>
        CommandModelReader.Read(
            Name,
            args =>
                Run(args)
                    .Replace(
                        "Group the rows by this column.",
                        "Group the rows by this column or tag.",
                        StringComparison.Ordinal
                    )
        );
}
