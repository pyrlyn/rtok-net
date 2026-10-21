using System.Globalization;
using System.Runtime.CompilerServices;
using CliWrap;
using Rtok.Config;

namespace Rtok.Log.Tests;

/// <summary>
/// The test assembly doubles as a writer process: started with <see cref="Variable"/> set, it
/// logs its lines through the Serilog pipeline from the module initializer and exits before the
/// test host runs.
/// </summary>
internal static class WriterProcess
{
    private const string Variable = "RTOK_LOG_TESTS_WRITER";

    [ModuleInitializer]
    internal static void RunWhenAsked()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { } spec)
        {
            return;
        }
        // path|maxBytes|files|writer|lines
        var parts = spec.Split('|');
        var inv = CultureInfo.InvariantCulture;
        var section = new LogSection
        {
            Path = parts[0],
            MaxBytes = ulong.Parse(parts[1], inv),
            Files = uint.Parse(parts[2], inv),
            Level = "info",
            ToDb = false,
        };
        var writer = parts[3];
        var lines = int.Parse(parts[4], inv);
        using (var log = RtokLog.Create(section, new RtokLogOptions { Debug = "off" }))
        {
            var named = log.ForSource("writer").Named(writer);
            for (var i = 0; i < lines; i++)
            {
                named.Information("{Writer}:{Line}", writer, i);
            }
        }
        Environment.Exit(0);
    }

    /// <summary>Runs a writer to its exit.</summary>
    internal static Task<CommandResult> Run(
        string path,
        long maxBytes,
        int files,
        string writer,
        int lines
    )
    {
        var host = Environment.ProcessPath!;
        var dotnet = string.Equals(
            Path.GetFileNameWithoutExtension(host),
            "dotnet",
            StringComparison.Ordinal
        );
        return Cli.Wrap(host)
            .WithArguments(dotnet ? [typeof(WriterProcess).Assembly.Location] : [])
            .WithEnvironmentVariables(e =>
                e.Set(
                    Variable,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{path}|{maxBytes}|{files}|{writer}|{lines}"
                    )
                )
            )
            .ExecuteAsync(TestContext.Current.CancellationToken)
            .Task;
    }
}
