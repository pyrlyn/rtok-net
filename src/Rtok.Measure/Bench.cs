using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Pyrlyn.Config.Layered;
using Rtok.Agents.Usage;
using Rtok.Config;
using Rtok.Process;
using Tomlyn.Model;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok bench</c>: every task of <c>[bench] tasks</c> run through the agent once per
/// <c>[bench.configs]</c> settings file, <c>runs</c> times, as <c>&lt;agent&gt; -p &lt;prompt&gt;
/// --output-format json --settings &lt;file&gt;</c> with stdin closed, the run timeout and an
/// 8 MiB output cap. Prints the mean tokens and cost per config, and writes them to
/// <c>results/&lt;config&gt;.json</c> beside the task file. A run that fails, times out or
/// prints no JSON is counted as failed and left out of the means.
/// </summary>
public static class Bench
{
    /// <summary>The most output kept from one run: its JSON result is a few KiB, so more is a runaway.</summary>
    public const long MaxOutput = 8L << 20;

    /// <summary>The bench's output.</summary>
    /// <param name="config">The loaded config: <c>[bench]</c>.</param>
    /// <param name="agent">The agent program: <c>claude</c>, or a path.</param>
    /// <param name="cancellationToken">Stops the runs.</param>
    public static async Task<string> RunAsync(
        RtokConfig config,
        string agent,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        var bench = config.Bench;
        if (bench.Suite.Length > 0)
        {
            // The graph suite compares `rtok mcp` with native reads; it comes with the MCP server.
            throw new InvalidOperationException($"bench.suite: unknown suite \"{bench.Suite}\"");
        }
        var tasks = Tasks(bench.Tasks);
        var configs = bench.Configs.OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
        if (bench.DryRun)
        {
            var plan = new StringBuilder();
            foreach (var t in tasks)
            {
                foreach (var (name, _) in configs)
                {
                    for (var n = 1; n <= bench.Runs; n++)
                    {
                        plan.Append(CultureInfo.InvariantCulture, $"{t.Id} {name} {n}\n");
                    }
                }
            }
            return plan.ToString();
        }
        foreach (var (name, settings) in configs.Where(c => !File.Exists(c.Value)))
        {
            throw new InvalidOperationException(
                $"bench.configs.{name}: no settings file at {settings}"
            );
        }
        var rows = await RowsAsync(tasks, configs, agent, bench, cancellationToken)
            .ConfigureAwait(false);
        Save(rows, tasks.Count, bench.Tasks);
        return Table(rows, tasks.Count);
    }

    /// <summary>The <c>[[tasks]]</c> of <paramref name="path"/>; each needs <c>id</c>, <c>prompt</c> and <c>check</c>.</summary>
    internal static List<BenchTask> Tasks(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"bench.tasks: {path}: {e.Message}", e);
        }
        TomlTable root;
        try
        {
            root = TomlText.Parse(text, path);
        }
        catch (ConfigFileException e)
        {
            throw new InvalidOperationException($"bench.tasks: {e.Message}", e);
        }
        var tasks = new List<BenchTask>();
        if (root.TryGetValue("tasks", out var node) && node is TomlTableArray array)
        {
            foreach (var t in array)
            {
                tasks.Add(
                    new BenchTask(
                        Field(t, "id", path, tasks.Count),
                        Field(t, "prompt", path, tasks.Count),
                        Field(t, "check", path, tasks.Count)
                    )
                );
            }
        }
        return tasks.Count > 0
            ? tasks
            : throw new InvalidOperationException($"bench.tasks: no [[tasks]] in {path}");
    }

    private static string Field(TomlTable t, string key, string path, int index) =>
        t.TryGetValue(key, out var v) && v is string s
            ? s
            : throw new InvalidOperationException(
                $"bench.tasks: {path}: task {index + 1} has no string `{key}`"
            );

    private static async Task<SortedDictionary<string, BenchRow>> RowsAsync(
        List<BenchTask> tasks,
        List<KeyValuePair<string, string>> configs,
        string agent,
        BenchSection bench,
        CancellationToken ct
    )
    {
        var rows = new SortedDictionary<string, BenchRow>(StringComparer.Ordinal);
        var timeout = TimeSpan.FromSeconds(Math.Max(bench.TimeoutS, 1));
        var bar = ProgressRunner.Batch(
            "bench",
            tasks.Count * configs.Count * (long)bench.Runs,
            ProgressUnit.Items
        );
        try
        {
            foreach (var t in tasks)
            {
                foreach (var (name, settings) in configs)
                {
                    var row = rows.TryGetValue(name, out var r) ? r : rows[name] = new BenchRow();
                    for (var n = 1; n <= bench.Runs; n++)
                    {
                        bar.Step($"{t.Id} {name} {n}/{bench.Runs}");
                        row.Add(
                            await OneAsync(agent, t.Prompt, settings, timeout, ct)
                                .ConfigureAwait(false)
                        );
                    }
                    if (await PassesAsync(t, ct).ConfigureAwait(false))
                    {
                        row.Pass++;
                    }
                }
            }
        }
        finally
        {
            bar.Finish();
        }
        return rows;
    }

    /// <summary><c>(input, cache, output, cost)</c> of one run, or null when it gave no result.</summary>
    internal static async Task<(long, long, long, double)?> OneAsync(
        string agent,
        string prompt,
        string settings,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        ProcessResult done;
        try
        {
            done = await new ProgressRunner(
                "",
                agent,
                ["-p", prompt, "--output-format", "json", "--settings", settings]
            )
                .NoProgress()
                .WindowsShim()
                .Timeout(timeout)
                .RunCappedAsync(MaxOutput, ct)
                .ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException or Win32Exception)
        {
            return null;
        }
        return done.ExitCode == 0 ? Usage(done.Stdout) : null;
    }

    /// <summary>The usage and <c>total_cost_usd</c> of <c>claude -p --output-format json</c>.</summary>
    internal static (long, long, long, double)? Usage(byte[] stdout)
    {
        if (
            Json.Parse(Encoding.UTF8.GetString(stdout)) is not { ValueKind: JsonValueKind.Object } v
        )
        {
            return null;
        }
        var cost =
            Json.At(v, "total_cost_usd") is { ValueKind: JsonValueKind.Number } c
            && c.TryGetDouble(out var d)
            && double.IsFinite(d)
            && d >= 0
                ? d
                : 0;
        return (
            Json.Count(v, "usage", "input_tokens"),
            Json.Count(v, "usage", "cache_read_input_tokens")
                + Json.Count(v, "usage", "cache_creation_input_tokens"),
            Json.Count(v, "usage", "output_tokens"),
            cost
        );
    }

    /// <summary>Whether the task's <c>check</c> exits 0 under <c>sh -c</c>; its output is kept, not shown.</summary>
    private static async Task<bool> PassesAsync(BenchTask t, CancellationToken ct)
    {
        try
        {
            var done = await ProgressRunner
                .Quiet("sh", ["-c", t.Check])
                .RunAsync(ct)
                .ConfigureAwait(false);
            return done.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static string Table(SortedDictionary<string, BenchRow> rows, int tasks)
    {
        var sb = new StringBuilder();
        sb.Append(
            CultureInfo.InvariantCulture,
            $"{"config", -8} {"input", 10} {"cache", 10} {"output", 10} {"cost", 10} {"pass", 8} {"failed", 6}\n"
        );
        foreach (var (name, r) in rows)
        {
            var n = Math.Max(r.Runs, 1);
            sb.Append(
                CultureInfo.InvariantCulture,
                $"{name, -8} {(double)r.Input / n, 10:0} {(double)r.Cache / n, 10:0} {(double)r.Output / n, 10:0} {r.Cost / n, 10:0.0000} {r.Pass, 4}/{tasks, -3} {r.Failed, 6}\n"
            );
        }
        return sb.ToString();
    }

    /// <summary><c>results/&lt;config&gt;.json</c> beside the task file; a write that fails leaves the table as the result.</summary>
    private static void Save(SortedDictionary<string, BenchRow> rows, int tasks, string taskFile)
    {
        var dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(taskFile)) ?? ".", "results");
        try
        {
            Directory.CreateDirectory(dir);
            foreach (var (name, r) in rows)
            {
                var n = Math.Max(r.Runs, 1);
                var buffer = new ArrayBufferWriter<byte>();
                using (
                    var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true })
                )
                {
                    w.WriteStartObject();
                    w.WriteString("config", name);
                    w.WriteNumber("runs", r.Runs);
                    w.WriteNumber("failed", r.Failed);
                    w.WriteNumber("mean_input", (double)r.Input / n);
                    w.WriteNumber("mean_cache", (double)r.Cache / n);
                    w.WriteNumber("mean_output", (double)r.Output / n);
                    w.WriteNumber("mean_cost_usd", r.Cost / n);
                    w.WriteNumber("pass", r.Pass);
                    w.WriteNumber("tasks", tasks);
                    w.WriteEndObject();
                }
                File.WriteAllBytes(
                    Path.Combine(dir, name + ".json"),
                    [.. buffer.WrittenSpan, (byte)'\n']
                );
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The printed table is the result; the files are a convenience.
        }
    }
}
