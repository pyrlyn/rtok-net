using System.Security.Cryptography;
using System.Text;
using Pyrlyn.ChangePreview;

namespace Rtok.Doctor;

/// <summary>
/// One <c>--fix</c> pass over a fresh scan: the kept-copy swaps and toggles of a request
/// applied, what is selected, what is refused, and each file's planned edit.
/// </summary>
internal sealed class FixSession
{
    private readonly List<bool> _on;

    public FixSession(DoctorHome home, FixRequest request)
    {
        Home = home;
        Findings = Findings.Scan(home);
        Kinds = (request.Kinds ?? ProblemKinds.Fixed).ToHashSet(StringComparer.Ordinal);
        Problems = [.. Findings.Problems];
        foreach (var keep in request.Keep)
        {
            var at = Problems.FindIndex(p => Is(p, keep) && p.Group is not null);
            if (at >= 0)
            {
                _ = KeepCopy(Problems, at);
            }
        }
        _on = [.. Problems.Select(p => Findings.Removable(p, Kinds) && !Findings.Shared(p.Source))];
        for (var i = 0; i < Problems.Count; i++)
        {
            if (
                request.Toggled.Any(t => Is(Problems[i], t))
                && Findings.Removable(Problems[i], Kinds)
            )
            {
                _on[i] = !_on[i];
            }
        }
        var lost = Findings.Lost(Problems, _on);
        foreach (var i in lost)
        {
            _on[i] = false;
        }
        Files = Plan();
    }

    public DoctorHome Home { get; }

    public Findings Findings { get; }

    public HashSet<string> Kinds { get; }

    public List<Problem> Problems { get; }

    public List<FileFix> Files { get; }

    /// <summary>The selected entries, one per place, in scan order.</summary>
    public List<Problem> Selected =>
        [.. Problems.Where((p, i) => _on[i]).DistinctBy(p => (p.Source, p.Path))];

    /// <summary>Every removable entry, selected or not, one per place.</summary>
    public List<FixItem> Items()
    {
        var selected = Selected.Select(p => (p.Source, p.Path)).ToHashSet();
        return
        [
            .. Problems
                .Where(p => Findings.Removable(p, Kinds))
                .DistinctBy(p => (p.Source, p.Path))
                .Select(p => new FixItem(
                    p.Source,
                    p.Path,
                    p.Kind,
                    p.Agent,
                    Label(p),
                    p.Command ?? "",
                    p.Detail,
                    KeptIn(p),
                    Findings.Shared(p.Source),
                    selected.Contains((p.Source, p.Path)),
                    p.Group is not null && p.Detail.StartsWith("runs ", StringComparison.Ordinal)
                )),
        ];
    }

    /// <summary>The wanted entries <c>--fix</c> will not touch, and why.</summary>
    public List<string> Refused() =>
        [
            .. Problems
                .Where(p => Findings.Wanted(p, Kinds) && (!p.Fixable || Findings.IsOwn(p)))
                .DistinctBy(p => (p.Source, p.Path))
                .Select(p => $"not removed: {Label(p)} `{p.Command}` in {p.Source}: {Why(p)}"),
        ];

    private string Why(Problem p)
    {
        if (Findings.IsOwn(p))
        {
            return "rtok's own entry: `rtok agents install` rewrites it";
        }
        return
            p.Source.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            && p.Kind is not ProblemKinds.DuplicateMcp
            ? "TOML hook files are not edited yet"
            : "not a file of yours to edit";
    }

    /// <summary>The diffs of every planned file, or why a file would be skipped.</summary>
    public string Diffs() =>
        string.Concat(
            Files.Select(f => f.Error is null ? f.Diff : $"{f.Source}: skipped: {f.Error}\n")
        );

    /// <summary>The hex SHA-256 of the selected places and the diffs: what a confirmation names.</summary>
    public string Token()
    {
        var sb = new StringBuilder();
        foreach (var p in Selected)
        {
            sb.Append(p.Source).Append('\0').Append(p.Path).Append('\0');
        }
        sb.Append('\0').Append(Diffs());
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    public FixPlan ToPlan() => new(Items(), Diffs(), Refused(), Token());

    /// <summary>The planned edit of each file the selection touches.</summary>
    private List<FileFix> Plan() =>
        [
            .. Selected
                .GroupBy(p => p.Source, StringComparer.Ordinal)
                .Select(file => Planned(file.Key, file.ToList())),
        ];

    /// <summary>One file's edit, or why it cannot be made.</summary>
    private FileFix Planned(string source, List<Problem> entries)
    {
        var hooks = entries.Where(p => p.Kind is not ProblemKinds.DuplicateMcp).ToList();
        var mcp = entries
            .Where(p => p.Kind is ProblemKinds.DuplicateMcp)
            .Select(Findings.McpAt)
            .OfType<McpEntry>()
            .ToList();
        string? before;
        try
        {
            before = Home.Read(source);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            return new FileFix(source, entries, "", "", "", $"cannot read {source}: {e.Message}");
        }
        if (before is null)
        {
            return new FileFix(source, entries, "", "", "", $"cannot read {source}: it is gone");
        }
        try
        {
            var after = FixEdit.Apply(source, before, hooks, mcp);
            return new FileFix(
                source,
                entries,
                before,
                after,
                UnifiedDiff.Render(source, before, after),
                null
            );
        }
        catch (InvalidDataException e)
        {
            return new FileFix(source, entries, before, before, "", e.Message);
        }
    }

    /// <summary>The checklist label: <c>Stop</c>, <c>PreToolUse[Bash]</c>, <c>mcp mcpServers.x</c>.</summary>
    internal static string Label(Problem p) =>
        p.Kind is ProblemKinds.DuplicateMcp or ProblemKinds.ConflictingMcp or ProblemKinds.OwnMcp
            ? $"mcp {p.Path}"
        : p.Matcher is { } m ? $"{p.Event}[{m}]"
        : p.Event ?? p.Path;

    /// <summary>The file of the copy a duplicate keeps; null outside a duplicate.</summary>
    private string? KeptIn(Problem p) =>
        p.Group is null || p.Keep
            ? null
            : Problems
                .FirstOrDefault(q =>
                    q.Group == p.Group
                    && string.Equals(q.Kind, p.Kind, StringComparison.Ordinal)
                    && q.Keep
                )
                ?.Source;

    /// <summary>
    /// Keeps the copy at <paramref name="at"/> instead of its group's kept one; the old kept copy
    /// becomes the extra. Null on success, else why it cannot be done.
    /// </summary>
    internal static string? KeepCopy(List<Problem> problems, int at)
    {
        var chosen = problems[at];
        if (
            chosen.Group is null
            || chosen.Kind is not (ProblemKinds.DuplicateHook or ProblemKinds.DuplicateMcp)
        )
        {
            return "only a copy of a duplicate has a kept copy";
        }
        if (!chosen.Detail.StartsWith("runs ", StringComparison.Ordinal))
        {
            return "the host decides which of these is used; it cannot be changed here";
        }
        if (chosen.Keep)
        {
            return "that copy is not part of a duplicate";
        }
        var old = problems.FindIndex(p =>
            p.Group == chosen.Group
            && string.Equals(p.Kind, chosen.Kind, StringComparison.Ordinal)
            && p.Keep
        );
        if (old < 0)
        {
            return "that copy is not part of a duplicate";
        }
        var kept = problems[old];
        if (!kept.Editable)
        {
            return "the kept copy is not yours to edit, so it stays";
        }
        var cut = kept.Detail.IndexOf(" (", StringComparison.Ordinal);
        var head = cut < 0 ? kept.Detail : kept.Detail[..cut];
        var extra = chosen.Detail;
        problems[at] = chosen with
        {
            Keep = true,
            Fixable = false,
            Detail = $"{head} (your choice)",
        };
        problems[old] = kept with { Keep = false, Fixable = kept.Editable, Detail = extra };
        return null;
    }

    private static bool Is(Problem p, FixRef r) =>
        string.Equals(p.Source, r.Source, StringComparison.Ordinal)
        && string.Equals(p.Path, r.Path, StringComparison.Ordinal);
}
