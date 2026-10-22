using Rtok.Measure;

namespace Rtok.Report;

/// <summary>Builds the sections once, in their fixed order: Window, Savings, Calls, Cache,
/// Expand, Config, Doctor, Recommendations. Every saving figure comes from Measurement rows.</summary>
public static class ReportBuilder
{
    /// <summary>The document title.</summary>
    public const string Title = "rtok report";

    private const string NoRows = "No rows in window.";

    /// <summary>The report of <paramref name="facts"/>.</summary>
    /// <param name="facts">What the store holds for the window.</param>
    /// <param name="config">Every effective config key with its origin.</param>
    /// <param name="doctor">The doctor page's text.</param>
    public static ReportDocument Build(
        ReportFacts facts,
        IReadOnlyList<ConfigLine> config,
        string doctor
    )
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(doctor);
        return new ReportDocument(
            Title,
            facts.Window,
            [
                Window(facts),
                Savings(facts),
                Calls(facts),
                Cache(facts),
                Expand(facts),
                Config(config),
                Section(
                    "Doctor",
                    "units: live probes, not store rows",
                    P("Live probes (the ", C("rtok doctor"), " page), not store rows."),
                    new CodeBlock(doctor)
                ),
                Section(
                    "Recommendations",
                    "units: rules over ledgers, most recoverable tokens first",
                    new Findings(Advice.Of(facts))
                ),
            ]
        );
    }

    private static ReportSection Window(ReportFacts f)
    {
        var blocks = new List<ReportBlock>
        {
            new Table(
                [new("ledger"), new("in_window", Numeric: true), new("total", Numeric: true)],
                [
                    ["calls", Fmt.Num(f.Calls), Fmt.Num(f.CallsTotal)],
                    ["measurements", Fmt.Num(f.Measurements), Fmt.Num(f.MeasurementsTotal)],
                    ["usage", Fmt.Num(f.Usage), Fmt.Num(f.UsageTotal)],
                ]
            ),
        };
        if (f.Calls == 0 && f.Measurements == 0 && f.Usage == 0)
        {
            blocks.Add(
                P(S("No rows in window.", SpanKind.Strong), " The store has no rows to report.")
            );
        }
        return Section("Window", "units: row counts per ledger", [.. blocks]);
    }

    private static ReportSection Savings(ReportFacts f)
    {
        const string Units =
            "units: est tokens, floors from Measurement rows (no turn counts); expand rows negative";
        var intro = P(
            "Estimated tokens from ",
            C("Measurement"),
            " rows only: est_before − est_after, net — an ",
            C("expand"),
            " row counts negative, because retrieval costs tokens. A ",
            C("Measurement"),
            " row carries no turn count, so these are floors, not context-token-turns."
        );
        if (f.Savings.Count == 0)
        {
            return Section("Savings", Units, intro, P(NoRows));
        }
        var rows = Saturating.Sum(f.Savings.Select(s => s.Rows));
        var saved = Saturating.Sum(f.Savings.Select(s => s.Saved));
        return Section(
            "Savings",
            Units,
            intro,
            new Table(
                [
                    new("plugin"),
                    new("rows", Numeric: true),
                    new("est_before", Numeric: true),
                    new("est_after", Numeric: true),
                    new("saved", Numeric: true),
                ],
                [
                    .. f.Savings.Select(s =>
                        (IReadOnlyList<string>)
                            [
                                s.Plugin,
                                Fmt.Num(s.Rows),
                                Fmt.Num(s.EstBefore),
                                Fmt.Num(s.EstAfter),
                                Fmt.Num(s.Saved),
                            ]
                    ),
                ]
            ),
            new BarChart(
                "saved tokens per plugin",
                [.. f.Savings.Select(s => new Bar(s.Plugin, s.Saved))]
            ),
            P(
                $"Total: {Fmt.Num(saved)} est tokens over {Fmt.Num(rows)} ",
                C("Measurement"),
                " rows in window."
            )
        );
    }

    private static ReportSection Calls(ReportFacts f)
    {
        var blocks = new List<ReportBlock>
        {
            P("Latency per surface, nearest-rank p50/p95 over the calls that recorded one."),
            new Table(
                [
                    new("surface"),
                    new("calls", Numeric: true),
                    new("timed", Numeric: true),
                    new("p50_ms", Numeric: true),
                    new("p95_ms", Numeric: true),
                ],
                [
                    .. f.Surfaces.Select(s =>
                        (IReadOnlyList<string>)
                            [
                                s.Surface,
                                Fmt.Num(s.Calls),
                                Fmt.Num(s.Timed),
                                Fmt.Ms(s.P50),
                                Fmt.Ms(s.P95),
                            ]
                    ),
                ]
            ),
            new BarChart(
                "calls per surface",
                [.. f.Surfaces.Select(s => new Bar(s.Surface, s.Calls))]
            ),
        };
        if (f.Hooks.Count > 0)
        {
            blocks.Add(
                new Table(
                    [new("hook"), new("calls", Numeric: true), new("measured", Numeric: true)],
                    [
                        .. f.Hooks.Select(h =>
                            (IReadOnlyList<string>)[h.Name, Fmt.Num(h.Calls), Fmt.Num(h.Measured)]
                        ),
                    ]
                )
            );
        }
        blocks.Add(
            P(
                $"{Fmt.Num(f.Calls)} of {Fmt.Num(f.CallsTotal)} ",
                C("calls"),
                $" rows in window ({f.Window.Since}); surfaces beyond hook/mcp/proxy are not in the section set."
            )
        );
        return Section(
            "Calls",
            "units: counts; p50_ms/p95_ms nearest-rank over timed calls, blank when untimed",
            [.. blocks]
        );
    }

    private static ReportSection Cache(ReportFacts f)
    {
        const string Units = "units: counts from usage rows in window";
        var intro = P("Prompt-cache busts by cause, from the proxy's ", C("usage"), " rows.");
        if (f.Cache.Count == 0)
        {
            return Section("Cache", Units, intro, P(NoRows));
        }
        var causes = f
            .Cache.SelectMany(s => s.Turns)
            .Select(t => t.Bust)
            .OfType<string>()
            .GroupBy(c => c, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new Bar(g.Key, g.Count()));
        var busts = f.Cache.Sum(s => (long)s.Busts);
        var turns = f.Cache.Sum(s => (long)s.Turns.Count);
        return Section(
            "Cache",
            Units,
            intro,
            new Table(
                [
                    new("session"),
                    new("turns", Numeric: true),
                    new("cache_read", Numeric: true),
                    new("cache_create", Numeric: true),
                    new("busts", Numeric: true),
                ],
                [.. f.Cache.Select(CacheRow)]
            ),
            new BarChart("busts per cause", [.. causes]),
            P(
                $"Busts: {Fmt.Num(busts)} over {Fmt.Num(turns)} turns in {Fmt.Num(f.Cache.Count)} session(s)."
            )
        );
    }

    private static IReadOnlyList<string> CacheRow(CacheHealth.Session s) =>
        [
            s.Id,
            Fmt.Num(s.Turns.Count),
            Fmt.Num(s.CacheRead),
            Fmt.Num(s.CacheCreate),
            Fmt.Num(s.Busts),
        ];

    private static ReportSection Expand(ReportFacts f)
    {
        var e = f.Expand;
        return Section(
            "Expand",
            "units: counts; rate = expanded/decisions",
            P(
                "How often a live-zone pointer had to be expanded (",
                C("archive_decisions"),
                " rows)."
            ),
            new Table(
                [
                    new("decisions", Numeric: true),
                    new("expanded", Numeric: true),
                    new("rate", Numeric: true),
                    new("cost", Numeric: true),
                    new("cost_rows", Numeric: true),
                ],
                [
                    [
                        Fmt.Num(e.Decisions),
                        Fmt.Num(e.Expanded),
                        Fmt.Percent(e.Rate),
                        Fmt.Num(e.Cost),
                        Fmt.Num(e.CostRows),
                    ],
                ]
            ),
            P(
                C("rtok expand"),
                $" froze {Fmt.Num(e.Expanded)} of {Fmt.Num(e.Decisions)} live-zone pointers ({Fmt.Percent(e.Rate)}). Expanded: {(e.ExpandedIds.Count == 0 ? "none" : string.Join(", ", e.ExpandedIds))}."
            )
        );
    }

    private static ReportSection Config(IReadOnlyList<ConfigLine> config) =>
        Section(
            "Config",
            "units: effective keys with origin",
            P("Every effective key with its origin — the ", C("config show --sources"), " page."),
            new Table(
                [new("key"), new("value"), new("source")],
                [.. config.Select(c => (IReadOnlyList<string>)[c.Key, c.Value, c.Source])]
            ),
            P($"{Fmt.Num(config.Count)} keys.")
        );

    private static ReportSection Section(string title, string units, params ReportBlock[] blocks) =>
        new(title.ToLowerInvariant(), title, units, blocks);

    private static Paragraph P(params object[] parts) =>
        new([.. parts.Select(p => p as Span ?? new Span((string)p))]);

    private static Span C(string text) => new(text, SpanKind.Code);

    private static Span S(string text, SpanKind kind) => new(text, kind);
}
