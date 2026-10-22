using Rtok.Config;
using Rtok.Measure;

namespace Rtok.Cli;

/// <summary><c>rtok bench</c>: the A/B harness, claude -p per task and settings file.</summary>
internal static class BenchCommands
{
    /// <summary>Run each bench task with claude -p under each [bench.configs] settings file and print mean tokens and cost per config.</summary>
    /// <param name="tasks">The task file; [bench] tasks when omitted.</param>
    /// <param name="runs">Runs per task and config.</param>
    /// <param name="dryRun">Print the planned runs; run nothing.</param>
    /// <param name="timeout">Seconds one run may take before it is killed.</param>
    /// <param name="suite">The task suite; empty for the task file.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="cancellationToken">Ctrl-C.</param>
    public static async Task RunAsync(
        string? tasks = null,
        uint? runs = null,
        bool dryRun = false,
        ulong? timeout = null,
        string? suite = null,
        string? config = null,
        CancellationToken cancellationToken = default
    )
    {
        var flags = new List<ConfigFlag>();
        void Flag(string key, object? value, string name)
        {
            if (value is not null)
            {
                flags.Add(new ConfigFlag($"bench.{key}", value, name));
            }
        }
        Flag("tasks", tasks, "--tasks");
        Flag("runs", (long?)runs, "--runs");
        Flag("dry_run", dryRun ? true : null, "--dry-run");
        Flag("timeout_s", (long?)timeout, "--timeout");
        Flag("suite", suite, "--suite");
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        Console.Out.Write(
            await Bench.RunAsync(loaded.Config, "claude", cancellationToken).ConfigureAwait(false)
        );
    }
}
