using System.Text.Encodings.Web;
using System.Text.Json;
using Rtok.Cli.Ui;
using Rtok.Config;
using Rtok.Log;

namespace Rtok.Cli;

/// <summary><c>rtok logs</c>: rtok's own log, newest first across the rotated files.</summary>
internal static class LogsCommands
{
    /// <summary>Print the last lines of rtok's log, numbered 1 = newest, across the rotated files.</summary>
    /// <param name="lines">How many lines; [log] lines when omitted.</param>
    /// <param name="json">Print the lines as a JSON array, newest first.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    public static void Show(uint? lines = null, bool json = false, string? config = null)
    {
        var log = Section(config);
        var tail = LogTail.Read(log.Path, Count(lines, log));
        Print(tail, json, LogScreen.Numbered(tail, Lines.ColorOn(stderr: false)));
    }

    /// <summary>Print the same lines as rtok logs without numbers or colour.</summary>
    /// <param name="lines">How many lines; [log] lines when omitted.</param>
    /// <param name="json">Print the lines as a JSON array, newest first.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    public static void Export(uint? lines = null, bool json = false, string? config = null)
    {
        var log = Section(config);
        var tail = LogTail.Read(log.Path, Count(lines, log));
        Print(tail, json, tail);
    }

    /// <summary>Show rtok logs, then follow the log until Ctrl-C: repainted on a terminal, appended when piped.</summary>
    /// <param name="lines">How many lines the screen keeps; [log] lines when omitted.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="cancellationToken">Ctrl-C.</param>
    public static void Watch(
        uint? lines = null,
        string? config = null,
        CancellationToken cancellationToken = default
    )
    {
        var log = Section(config);
        LogWatch.Run(
            log.Path,
            Count(lines, log),
            Console.Out,
            !Console.IsOutputRedirected,
            Lines.ColorOn(stderr: false),
            cancellationToken
        );
    }

    private static LogSection Section(string? config) =>
        ConfigLoader.Load(new ConfigLoadOptions { ConfigFile = config }).Config.Log;

    private static int Count(uint? lines, LogSection log) =>
        (int)Math.Min(lines ?? log.Lines, int.MaxValue);

    private static void Print(IReadOnlyList<string> tail, bool json, IReadOnlyList<string> rows)
    {
        if (json)
        {
            PrintJson(tail);
            return;
        }
        if (tail.Count == 0)
        {
            Lines.Info(LogScreen.Empty);
            return;
        }
        var output = Console.Out;
        foreach (var row in rows)
        {
            output.Write(row);
            output.Write('\n');
        }
        output.Flush();
    }

    private static void PrintJson(IReadOnlyList<string> tail)
    {
        using var stdout = Console.OpenStandardOutput();
        using (
            var json = new Utf8JsonWriter(
                stdout,
                new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            )
        )
        {
            json.WriteStartArray();
            foreach (var line in tail)
            {
                json.WriteStringValue(line);
            }
            json.WriteEndArray();
        }
        stdout.WriteByte((byte)'\n');
    }
}
