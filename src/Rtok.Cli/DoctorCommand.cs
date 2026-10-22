using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Pyrlyn.Config.Layered;
using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Doctor;

namespace Rtok.Cli;

/// <summary><c>rtok doctor</c>: what the hosts load, what is broken or doubled, and the minimal fix.</summary>
internal static class DoctorCommand
{
    /// <summary>Check hooks, MCP servers, instructions and skills without running a hook; --fix removes broken and extra copies.</summary>
    /// <param name="json">Print JSON.</param>
    /// <param name="agent">Only this host's problems (claude, codex, …).</param>
    /// <param name="instructions">Also audit the instruction files and what plugins inject at SessionStart.</param>
    /// <param name="fix">Remove broken hooks and the extra copies of duplicates; a dry run without --yes.</param>
    /// <param name="yes">With --fix, write the edits (each file backed up first).</param>
    /// <param name="dryRun">With --fix, print the diffs and write nothing.</param>
    /// <param name="stat">With --fix, print stat lines instead of diffs.</param>
    /// <param name="only">With --fix, only these problems, comma-separated: broken-hooks, duplicate-hooks, duplicate-mcp.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    public static int Run(
        bool json = false,
        string? agent = null,
        bool instructions = false,
        bool fix = false,
        bool yes = false,
        bool dryRun = false,
        bool stat = false,
        string? only = null,
        string? config = null
    )
    {
        if (json && fix)
        {
            throw new ValidationException("--json and --fix cannot be used together");
        }
        if (!fix && (yes || dryRun || stat || only is not null))
        {
            throw new ValidationException("--yes, --dry-run, --stat and --only go with --fix");
        }
        if (agent is not null && AgentHosts.Find(agent) is null)
        {
            throw new ValidationException(
                $"unknown host `{agent}`; valid hosts: {string.Join(", ", AgentHosts.All.Select(h => h.Id))}"
            );
        }
        var kinds = only is null ? null : Kinds(only);
        IReadOnlyList<ConfigFlag> flags = instructions
            ? [new ConfigFlag("doctor.instructions", true, "--instructions")]
            : [];
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        foreach (var issue in loaded.Issues)
        {
            Console.Error.WriteLine($"warning: {issue} (using the default)");
        }
        var options = new DoctorOptions(
            HostContexts.Of(loaded, null),
            loaded.Config,
            Environment.CurrentDirectory
        )
        {
            Mcp = DoctorProbes.Mcp,
            Health = DoctorProbes.Health,
        };
        if (fix)
        {
            var (text, code) = DoctorFix.Run(options, kinds, apply: yes && !dryRun, stat);
            Console.Out.Write(text);
            return code;
        }
        var report = DoctorPage.Page(options);
        if (agent is not null)
        {
            report = DoctorPage.ForAgent(report, agent);
        }
        Console.Out.Write(
            json
                ? JsonSerializer.Serialize(report, DoctorJson.Default.DoctorReport) + "\n"
                : DoctorText.Render(report)
        );
        return 0;
    }

    private static List<string> Kinds(string only)
    {
        var kinds = new List<string>();
        foreach (
            var name in only.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            kinds.Add(
                ProblemKinds.Only.GetValueOrDefault(name)
                    ?? throw new ValidationException(
                        $"unknown problem `{name}`; name one of: {string.Join(", ", ProblemKinds.Only.Keys)}"
                    )
            );
        }
        return kinds;
    }
}
