namespace Rtok.Doctor;

/// <summary>Broken, suspect, unverified and duplicate hooks among a scan's entries.</summary>
internal static class HookFindings
{
    /// <summary>One problem per hook whose command does not check out.</summary>
    public static IEnumerable<Problem> Checked(IEnumerable<HookEntry> entries, CommandCheck check)
    {
        foreach (var e in entries)
        {
            var (verdict, detail) = check.Classify(e.Command, e.Source);
            var kind = verdict switch
            {
                Verdict.Broken => ProblemKinds.BrokenHook,
                Verdict.Suspect => ProblemKinds.SuspectHook,
                Verdict.Unverified => ProblemKinds.UnverifiedHook,
                _ => null,
            };
            if (kind is not null)
            {
                yield return Of(
                    e,
                    kind,
                    detail,
                    kind is ProblemKinds.BrokenHook && e.Source.Editable
                );
            }
        }
    }

    /// <summary>
    /// Hooks that run more than once per event: same host, event, matcher (order and <c>*</c>
    /// aside) and command (spelling aside). The copy in the lowest scope is kept, on a tie the
    /// first loaded; only a hand-written extra is fixable.
    /// </summary>
    public static IEnumerable<Problem> Duplicates(
        IReadOnlyList<HookEntry> entries,
        CommandCheck check,
        GroupCounter groups
    )
    {
        var copies = entries
            .Select((e, i) => (Entry: e, Order: i, Key: Key(e, check)))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Where(g => g.Skip(1).Any())
            .OrderBy(g => g.Min(x => x.Order));
        foreach (var group in copies)
        {
            var list = group.ToList();
            var kept = list.OrderBy(x => x.Entry.Source.Scope).ThenBy(x => x.Order).First();
            var why = list.TrueForAll(x => x.Entry.Source.Scope == kept.Entry.Source.Scope)
                ? "first in load order"
                : Why(kept.Entry.Source.Scope);
            var id = groups.Next();
            foreach (var (entry, order, _) in list)
            {
                var keep = order == kept.Order;
                var detail = keep
                    ? $"runs {list.Count} times; keep this copy ({why})"
                    : $"runs {list.Count} times; an extra copy";
                yield return Of(
                    entry,
                    ProblemKinds.DuplicateHook,
                    detail,
                    !keep && entry.Source.Editable
                ) with
                {
                    Group = id,
                    Keep = keep,
                };
            }
        }
    }

    /// <summary><c>Bash|Read</c> and <c>Read | Bash</c> are one matcher; <c>*</c> and none are one.</summary>
    internal static string MatcherKey(string? matcher)
    {
        var m = matcher?.Trim() ?? "";
        if (m.Length == 0 || m is "*")
        {
            return "";
        }
        var parts = m.Split('|', StringSplitOptions.TrimEntries);
        Array.Sort(parts, StringComparer.Ordinal);
        return string.Join('|', parts);
    }

    private static string Key(HookEntry e, CommandCheck check) =>
        string.Join(
            '\0',
            e.Agent,
            e.Event,
            MatcherKey(e.Matcher),
            check.Normalize(e.Command, e.Source)
        );

    private static string Why(Scope scope) =>
        scope switch
        {
            Scope.Managed => "managed settings",
            Scope.Plugin => "owned by a plugin",
            Scope.Project => "shared project file",
            Scope.User => "user file",
            _ => "local file",
        };

    private static Problem Of(HookEntry e, string kind, string detail, bool fixable) =>
        new(kind, e.Agent, e.Source.Path, e.Path, detail, fixable)
        {
            Event = e.Event,
            Matcher = e.Matcher,
            Command = e.Command,
            Editable = e.Source.Editable,
        };
}
