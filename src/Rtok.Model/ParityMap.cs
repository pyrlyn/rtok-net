using Pyrlyn.CommandLine.Docs;

namespace Rtok.Model;

/// <summary>
/// Every CLI read has a page on both web and TUI (rtok D27). A reading command is one with
/// <c>--json</c>; each maps to its pages here, or is exempt with a reason.
/// </summary>
public static class ParityMap
{
    /// <summary>Each reading command and its pages.</summary>
    public static IReadOnlyDictionary<string, PageLink> CommandPages { get; } =
        new Dictionary<string, PageLink>(StringComparer.Ordinal)
        {
            ["agents info"] = new(Page.Hosts, Page.Hosts),
            ["agents list"] = new(Page.Hosts, Page.Hosts),
            // An agent's junk sits with its host; `clear`'s dry run is the same breakdown.
            ["agents junk list"] = new(Page.Hosts, Page.Hosts),
            ["agents junk clear"] = new(Page.Hosts, Page.Hosts),
            ["agents usage"] = new(Page.Usage, Page.Usage),
            ["config get"] = new(Page.Config, Page.Config),
            ["config show"] = new(Page.Config, Page.Config),
            ["doctor"] = new(Page.Doctor, Page.Doctor),
            ["logs"] = new(Page.Logs, Page.Logs),
            ["logs export"] = new(Page.Logs, Page.Logs),
            ["plugins"] = new(Page.Plugins, Page.Plugins),
            ["stats"] = new(Page.Stats, Page.Stats),
            ["task list"] = new(Page.Tasks, Page.Tasks),
            ["task next"] = new(Page.Tasks, Page.Tasks),
            ["task ready"] = new(Page.Tasks, Page.Tasks),
            ["task show"] = new(Page.Tasks, Page.Tasks),
            ["task status"] = new(Page.Tasks, Page.Tasks),
            ["task sync"] = new(Page.Tasks, Page.Tasks),
        };

    /// <summary>Reading commands with no page, each with the reason.</summary>
    public static IReadOnlyDictionary<string, string> Exempt { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["task claim"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task conflict"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task create"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task dep"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task parallel"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task priority"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task release"] =
                "writes a task; --json prints the task it changed, which the Tasks page shows",
            ["task export"] = "writes to an issue tracker; --json prints what it sent",
        };

    /// <summary>True when the command reads: it has a <c>--json</c> option.</summary>
    /// <param name="command">The command.</param>
    /// <returns>Whether it reads.</returns>
    public static bool IsReading(CommandInfo command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Options.Any(o => o.Names.Contains("--json", StringComparer.Ordinal));
    }

    /// <summary>
    /// What breaks parity: a reading command with no page and no exemption, and a page or
    /// exempt entry whose command no longer exists.
    /// </summary>
    /// <param name="model">The command model.</param>
    /// <param name="pages">The command pages; <see cref="CommandPages"/> when null.</param>
    /// <param name="exempt">The exemptions; <see cref="Exempt"/> when null.</param>
    /// <returns>One line per problem; empty when parity holds.</returns>
    public static IReadOnlyList<string> Check(
        CommandModel model,
        IReadOnlyDictionary<string, PageLink>? pages = null,
        IReadOnlyDictionary<string, string>? exempt = null
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        pages ??= CommandPages;
        exempt ??= Exempt;
        var commands = model.Commands.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        problems.AddRange(
            model
                .Commands.Where(c =>
                    IsReading(c) && !pages.ContainsKey(c.Path) && !exempt.ContainsKey(c.Path)
                )
                .Select(c => $"rtok {c.Path} reads but has no web or TUI page and no exemption")
        );
        problems.AddRange(
            pages
                .Keys.Where(path => !commands.Contains(path))
                .Select(path => $"rtok {path} has pages but no such command")
        );
        problems.AddRange(
            exempt
                .Keys.Where(path => !commands.Contains(path))
                .Select(path => $"rtok {path} is exempt but no such command")
        );
        return problems;
    }
}
