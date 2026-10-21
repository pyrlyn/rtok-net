using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Pyrlyn.RotatingLog.Tests;

/// <summary>
/// The test assembly doubles as a writer process: started with <see cref="Variable"/> set, it
/// writes its lines from the module initializer and exits before the test host runs.
/// </summary>
internal static class WriterProcess
{
    private const string Variable = "PYRLYN_ROTATINGLOG_WRITER";

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
        var log = new RotatingFileLog(
            new RotatingFileLogOptions
            {
                Path = parts[0],
                MaxBytes = long.Parse(parts[1], inv),
                Files = int.Parse(parts[2], inv),
                Level = "debug",
                LockTimeout = TimeSpan.FromSeconds(30),
            }
        );
        var writer = parts[3];
        var lines = int.Parse(parts[4], inv);
        for (var i = 0; i < lines; i++)
        {
            log.Write(i % 10 == 0 ? "error" : "info", "writer", writer, $"{writer}:{i}");
        }
        Environment.Exit(0);
    }

    /// <summary>Starts a writer; dispose waits for nothing, call <see cref="Process.WaitForExit()"/>.</summary>
    internal static Process Start(string path, long maxBytes, int files, string writer, int lines)
    {
        var host = Environment.ProcessPath!;
        var start = new ProcessStartInfo(host) { UseShellExecute = false };
        if (
            string.Equals(
                Path.GetFileNameWithoutExtension(host),
                "dotnet",
                StringComparison.Ordinal
            )
        )
        {
            start.ArgumentList.Add(typeof(WriterProcess).Assembly.Location);
        }
        start.Environment[Variable] = string.Create(
            CultureInfo.InvariantCulture,
            $"{path}|{maxBytes}|{files}|{writer}|{lines}"
        );
        return Process.Start(start)!;
    }
}
