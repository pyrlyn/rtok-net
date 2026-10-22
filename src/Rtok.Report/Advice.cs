using System.Globalization;

namespace Rtok.Report;

/// <summary>The recommendation rules over the ledgers. Each finding names the rows it rests on;
/// the list is ordered by recoverable tokens, rule order breaking ties.</summary>
public static class Advice
{
    /// <summary>A hook event with at least this many calls and no Measurement row is weight on
    /// the hook path for nothing.</summary>
    public const int IdleHookCalls = 10;

    /// <summary>The findings of <paramref name="facts"/>, most recoverable tokens first.</summary>
    public static IReadOnlyList<Finding> Of(ReportFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        IEnumerable<Finding>[] rules =
        [
            ExpandRate(facts),
            RetirePlugin(facts),
            CacheBust(facts),
            IdleHook(facts),
            InjectBudget(facts),
            ArchiveWindow(facts),
            TopSinks(facts),
        ];
        // OrderByDescending is stable: equal tokens keep rule order.
        return [.. rules.SelectMany(r => r).OrderByDescending(f => f.Tokens)];
    }

    private static IEnumerable<Finding> ExpandRate(ReportFacts facts)
    {
        var e = facts.Expand;
        var max = facts.Settings.ExpandMaxRate;
        if (e.Decisions == 0 || !(e.Rate > max))
        {
            yield break;
        }
        yield return new Finding(
            "expand-rate",
            $"expand rate {Fmt.Percent(e.Rate)} ({Fmt.Num(e.Expanded)} of {Fmt.Num(e.Decisions)} live-zone pointers re-read) is above [expand] max_rate {Fmt.Percent(max)} — re-expansions cost {Fmt.Num(e.Cost)} est tokens over {Fmt.Count(e.CostRows, "row")}",
            $"{Fmt.Count(e.Decisions, "archive_decisions row")}, {Fmt.Count(e.CostRows, "archive Measurement row")} of kind expand",
            e.Cost
        );
    }

    private static IEnumerable<Finding> RetirePlugin(ReportFacts facts) =>
        facts
            .Savings.Where(s => s.Saved <= 0 && (s.EstBefore != 0 || s.EstAfter != 0))
            .Select(s => new Finding(
                "retire-plugin",
                $"plugin {s.Plugin} net {Fmt.Num(s.Saved)} est tokens over {Fmt.Count(s.Rows, "Measurement row")} (≤ 0) — retire it rather than stack it",
                Fmt.Count(s.Rows, $"{s.Plugin} Measurement row"),
                Saturating.Sub(0, s.Saved)
            ));

    private static IEnumerable<Finding> CacheBust(ReportFacts facts)
    {
        var busts = facts
            .Cache.SelectMany(s =>
                s.Turns.Select((t, i) => (Session: s.Id, Turn: i + 1, t))
                    .Where(x => x.t.Bust is "tools" or "system")
            )
            .GroupBy(x => (x.Session, Cause: x.t.Bust!));
        var evidence =
            $"{Fmt.Count(facts.Cache.Sum(s => (long)s.Busts), "cache bust")} over {Fmt.Count(facts.Cache.Sum(s => (long)s.Turns.Count), "turn")} in {Fmt.Count(facts.Cache.Count, "session")} with usage rows";
        foreach (var g in busts)
        {
            var (session, cause) = g.Key;
            var what = cause is "tools" ? "tool list" : "system prompt";
            var create = Saturating.Sum(g.Select(x => x.t.CacheCreate));
            var first = g.First();
            var text =
                g.Take(2).Count() == 1
                    ? $"session {session} turn {Fmt.Num(first.Turn)} busted the prompt cache (cause {cause}): {Fmt.Num(create)} cache-create tokens re-written with cache_read {Fmt.Num(first.t.CacheRead)} — pin the {what}; {Fmt.Num(create)} tokens would have stayed cached"
                    : $"session {session} busted the prompt cache {Fmt.Count(g.Count(), "time")} (cause {cause}, turns {Turns(g.Select(x => x.Turn).ToList())}): {Fmt.Num(create)} cache-create tokens re-written — pin the {what}; {Fmt.Num(create)} tokens would have stayed cached";
            yield return new Finding("cache-bust", text, evidence, create);
        }
    }

    private static string Turns(List<int> turns)
    {
        const int Shown = 5;
        var list = string.Join(", ", turns.Take(Shown).Select(t => Fmt.Num(t)));
        return turns.Count > Shown
            ? string.Create(CultureInfo.InvariantCulture, $"{list} and {turns.Count - Shown} more")
            : list;
    }

    private static IEnumerable<Finding> IdleHook(ReportFacts facts) =>
        facts
            .Hooks.Where(h => h.Calls >= IdleHookCalls && h.Measured == 0)
            .Select(h => new Finding(
                "idle-hook",
                $"{h.Name} fired {Fmt.Num(h.Calls)} times in window with no Measurement row on its path — weight on the 10 ms hook path for nothing; 0 tokens saved",
                $"{Fmt.Count(h.Calls, $"{h.Name} hook call")} in window, {Fmt.Count(facts.Measurements, "Measurement row")} in window",
                0
            ));

    private static IEnumerable<Finding> InjectBudget(ReportFacts facts)
    {
        var budget = facts.Settings.InjectBudget;
        if (facts.Savings.FirstOrDefault(s => s.Plugin is "inject") is not { Rows: > 0 } inject)
        {
            yield break;
        }
        var allowed = budget > long.MaxValue / inject.Rows ? long.MaxValue : budget * inject.Rows;
        if (inject.EstAfter <= allowed)
        {
            yield break;
        }
        var excess = Saturating.Sub(inject.EstAfter, allowed);
        yield return new Finding(
            "inject-budget",
            $"inject averages {Fmt.Num(inject.EstAfter / inject.Rows)} est tokens/turn over {Fmt.Count(inject.Rows, "inject Measurement row")} vs [plugins.inject] budget_tokens {Fmt.Num(budget)} — {Fmt.Num(excess)} tokens over budget; trim modes and memory recall",
            Fmt.Count(inject.Rows, "inject Measurement row"),
            excess
        );
    }

    private static IEnumerable<Finding> ArchiveWindow(ReportFacts facts)
    {
        var e = facts.Expand;
        if (e.Expanded == 0)
        {
            yield break;
        }
        var s = facts.Settings;
        yield return new Finding(
            "archive-window",
            $"archive keep_turns {Fmt.Num(s.KeepTurns)} with {Fmt.Num(e.Expanded)} of {Fmt.Num(e.Decisions)} pointers re-read via rtok expand ({Fmt.Num(e.Cost)} est tokens over {Fmt.Count(e.CostRows, "row")}) — old results ARE re-read; raise keep_turns or min_tokens ({Fmt.Num(s.MinTokens)}) so volatile results stay live",
            $"{Fmt.Count(e.Decisions, "archive_decisions row")}, {Fmt.Count(e.CostRows, "expand Measurement row")}",
            e.Cost
        );
    }

    private static IEnumerable<Finding> TopSinks(ReportFacts facts) =>
        facts.Sinks.Select(s => new Finding(
            "top-sinks",
            $"{s.Class} {s.Name} cost {Fmt.Num(s.Bytes)} bytes over {Fmt.Count(s.Rows, "Measurement row")} — {Switch(s, facts.Settings)}",
            $"{Fmt.Count(s.Rows, $"{s.Class} Measurement row")} for {s.Name}, {Fmt.Count(facts.Measurements, "Measurement row")} in window",
            Math.Max(
                1,
                (long)
                    Math.Min(
                        Math.Ceiling(s.Bytes / Math.Max(facts.Settings.CodeRate, 0.1)),
                        long.MaxValue
                    )
            )
        ));

    private static string Switch(Sink sink, AdviceSettings settings) =>
        sink.Class switch
        {
            "read" => settings.ReadMode is "full"
                ? "set [plugins.read] default_mode = \"map\""
                : $"read ranges instead ([plugins.read] default_mode = \"{settings.ReadMode}\")",
            "mcp" => $"wrap it: rtok mcp -- {sink.Name}",
            _ => $"add a [plugins.cmd] rules entry for `{sink.Name}`",
        };
}
