namespace Rtok.Config;

/// <summary>rtok bench</summary>
/// <remarks>Read from [bench].</remarks>
public sealed class BenchSection
{
    /// <summary>the task file</summary>
    public string Tasks { get; set; } = "bench/tasks.toml";

    /// <summary>runs per task and config</summary>
    public uint Runs { get; set; } = 3;

    /// <summary>print the plan and run nothing</summary>
    public bool DryRun { get; set; }

    /// <summary>per task run</summary>
    public ulong TimeoutS { get; set; } = 900;

    /// <summary>"" = the six default tasks; "graph" = with and without MCP</summary>
    public string Suite { get; set; } = "";

    /// <summary>free-form `name = settings file` pairs</summary>
    public IDictionary<string, string> Configs { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a"] = "bench/configs/legacy.json",
            ["b"] = "bench/configs/rtok.json",
        };
}
