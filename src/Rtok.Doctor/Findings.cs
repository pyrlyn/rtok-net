using Pyrlyn.Shlex;

namespace Rtok.Doctor;

/// <summary>One scan of every hook and MCP entry, and the problems in them.</summary>
internal sealed class Findings
{
    private Findings(DoctorHome home)
    {
        Home = home;
        Hooks = new HookScan(home);
        Check = new CommandCheck(home);
        Mcp = McpScan.Entries(home, Hooks.Plugins);
        var groups = new GroupCounter();
        Problems =
        [
            .. Hooks.Problems,
            .. HookFindings.Checked(Hooks.Entries, Check),
            .. HookFindings.Duplicates(Hooks.Entries, Check, groups),
            .. McpFindings.Find(home, Mcp, groups),
        ];
    }

    public DoctorHome Home { get; }

    public HookScan Hooks { get; }

    public CommandCheck Check { get; }

    public List<McpEntry> Mcp { get; }

    public List<Problem> Problems { get; }

    public static Findings Scan(DoctorHome home) => new(home);

    /// <summary>The MCP entry a problem names.</summary>
    public McpEntry? McpAt(Problem p) =>
        Mcp.FirstOrDefault(e =>
            string.Equals(e.Source, p.Source, StringComparison.Ordinal)
            && string.Equals(e.Path, p.Path, StringComparison.Ordinal)
        );

    /// <summary><c>--fix</c> looks at it: a broken hook, or the copy of a duplicate that is not kept.</summary>
    public static bool Wanted(Problem p, IReadOnlySet<string> kinds) =>
        kinds.Contains(p.Kind) && (p.Kind is ProblemKinds.BrokenHook || !p.Keep);

    /// <summary>
    /// rtok's own entry, which <c>rtok agents install</c> owns: <c>mcpServers.rtok</c>, or a hook
    /// that runs rtok or its hook client.
    /// </summary>
    public bool IsOwn(Problem p)
    {
        if (
            p.Kind
            is ProblemKinds.DuplicateMcp
                or ProblemKinds.ConflictingMcp
                or ProblemKinds.OwnMcp
        )
        {
            return p.Path[(p.Path.LastIndexOf('.') + 1)..] is "rtok";
        }
        return p.Command is { } cmd
            && ShellWords.TrySplit(cmd, out var words)
            && words.Any(w =>
                Home.Options.Rtok.Is(w) || CommandCheck.Base(w) is "rtok-hook" or "rtok-hook.cmd"
            );
    }

    /// <summary>A file the project shares: under the working directory, not the home, not a <c>.local</c> file.</summary>
    public bool Shared(string source) =>
        !string.Equals(Home.Cwd, Home.Home, StringComparison.Ordinal)
        && source.StartsWith(Home.Cwd, StringComparison.Ordinal)
        && !Home.Fs.Path.GetFileName(source).Contains(".local", StringComparison.Ordinal);

    /// <summary><c>--fix</c> may remove it: wanted, fixable and not rtok's own.</summary>
    public bool Removable(Problem p, IReadOnlySet<string> kinds) =>
        Wanted(p, kinds) && p.Fixable && !IsOwn(p);

    /// <summary>The problems with <see cref="Problem.Selected"/> set as <c>--fix</c> starts.</summary>
    public List<Problem> WithSelection()
    {
        var kinds = ProblemKinds.Fixed.ToHashSet(StringComparer.Ordinal);
        var on = Problems.Select(p => Removable(p, kinds) && !Shared(p.Source)).ToList();
        var lost = Lost(Problems, on);
        return [.. Problems.Select((p, i) => p with { Selected = on[i] && !lost.Contains(i) })];
    }

    /// <summary>
    /// The extras of a duplicate hook whose kept copy is about to go as broken: removing them too
    /// would leave no copy, so they stay.
    /// </summary>
    public static HashSet<int> Lost(IReadOnlyList<Problem> problems, IReadOnlyList<bool> on)
    {
        var broken = problems
            .Select((p, i) => (p, i))
            .Where(x => on[x.i] && x.p.Kind is ProblemKinds.BrokenHook)
            .Select(x => (x.p.Source, x.p.Path))
            .ToHashSet();
        var lostGroups = problems
            .Where(p =>
                p.Kind is ProblemKinds.DuplicateHook
                && p.Keep
                && broken.Contains((p.Source, p.Path))
            )
            .Select(p => p.Group)
            .ToHashSet();
        return
        [
            .. problems
                .Select((p, i) => (p, i))
                .Where(x =>
                    x.p.Kind is ProblemKinds.DuplicateHook
                    && !x.p.Keep
                    && lostGroups.Contains(x.p.Group)
                )
                .Select(x => x.i),
        ];
    }
}
