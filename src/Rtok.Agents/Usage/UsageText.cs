using System.Globalization;
using System.Text;

namespace Rtok.Agents.Usage;

/// <summary>The report as the default screen and as <c>--unpriced</c> prints it.</summary>
public static class UsageText
{
    private static readonly bool[] TextNumberNumber = [false, true, true];

    /// <summary>Header, summary, the unpriced warning, the agent (or model) table, then the month (or day) rows.</summary>
    public static string Render(UsageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var from = From(report);
        if (report.Through is not { } through)
        {
            return $"rtok agents usage: {from}, no usage recorded ({report.Tz})\n";
        }
        var t = report.Totals;
        var out_ = new StringBuilder()
            .Append($"rtok agents usage: {from}, up to {through} ({report.Tz})\n\n")
            .Append($"  {UsageUnits.Tokens(t.Row.Tokens)} tokens\n")
            .Append($"  {UsageUnits.Usd(t.Row.CostUsd)} estimated cost\n")
            .Append($"  {UsageUnits.Grouped(t.Sessions)} sessions\n")
            .Append($"  {UsageUnits.Grouped(t.DailyRows)} daily rows\n");
        if (t.Saved is { } s)
        {
            var worth = s.Usd is { } u ? $"≈ {UsageUnits.Usd(u)}" : "no price for an estimate";
            out_.Append($"  rtok saved {UsageUnits.Tokens(s.Tokens)} tokens ({worth})\n");
        }
        if (report.UnpricedModels > 0)
        {
            var n = report.UnpricedModels;
            out_.Append(
                    CultureInfo.InvariantCulture,
                    $"\n! Cost is incomplete: {n} model{(n == 1 ? " has" : "s have")} no price in [stats.prices], so {(n == 1 ? "its" : "their")} tokens are not in\n"
                )
                .Append("  the estimate. `rtok agents usage --unpriced` lists them.\n");
        }
        out_.Append('\n').Append(MiddleTable(report));
        var (label, head) = report.Daily ? ("Daily totals", "Day") : ("Monthly totals", "Month");
        List<IReadOnlyList<string>> rows =
        [
            [head, "Tokens", "Estimated cost"],
        ];
        rows.AddRange(report.Periods.Select(p => Cells(p.Period, p.Row)));
        return out_.Append($"\n{label}\n")
            .Append(TextTable.Render(TextNumberNumber, rows))
            .ToString();
    }

    /// <summary><c>--unpriced</c>: the models the estimate leaves out, with agent and tokens.</summary>
    public static string Unpriced(UsageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Unpriced.Count == 0)
        {
            return "every model has a price in [stats.prices]\n";
        }
        List<IReadOnlyList<string>> rows =
        [
            ["Model", "Agent", "Tokens"],
        ];
        rows.AddRange(
            report.Unpriced.Select(u =>
                (IReadOnlyList<string>)[u.Model, u.Host, UsageUnits.Tokens(u.Tokens)]
            )
        );
        return TextTable.Render([false, false, true], rows);
    }

    /// <summary>Where the numbers come from, as the header says it.</summary>
    private static string From(UsageReport report)
    {
        var n = report.Agents.Count;
        var logs = $"logs from {n} agent{(n == 1 ? "" : "s")}";
        return report.Source switch
        {
            "logs" => logs,
            "both" => $"{logs} and through rtok",
            _ => "through rtok",
        };
    }

    /// <summary>
    /// One row per model with <c>--by model</c>, else one per agent, with the <c>Through rtok</c> and
    /// <c>Coverage</c> (<c>both</c>) and <c>Saved</c> (<c>rtok</c>, <c>both</c>) columns.
    /// </summary>
    private static string MiddleTable(UsageReport report)
    {
        if (report.Models is { } models)
        {
            List<IReadOnlyList<string>> byModel =
            [
                ["Model", "Tokens", "Estimated cost"],
            ];
            byModel.AddRange(models.Select(m => Cells(m.Model, m.Row)));
            return TextTable.Render(TextNumberNumber, byModel);
        }
        var both = report.Source is "both";
        List<string> head = ["Agent", "Tokens", "Estimated cost"];
        if (both)
        {
            head.AddRange(["Through rtok", "Coverage"]);
        }
        if (report.Source is not "logs")
        {
            head.AddRange(["Saved tokens", "Saved est."]);
        }
        List<IReadOnlyList<string>> rows = [head];
        foreach (var a in report.Agents)
        {
            List<string> r = [.. Cells(a.Name, a.Row)];
            if (both)
            {
                r.Add(UsageUnits.Tokens(a.ThroughRtokTokens ?? 0));
                r.Add(a.Coverage is { } c ? UsageUnits.Fixed(c * 100, 0) + "%" : "-");
            }
            if (a.Saved is { } s)
            {
                r.AddRange([UsageUnits.Tokens(s.Tokens), UsageUnits.Usd(s.Usd)]);
            }
            rows.Add(r);
        }
        return TextTable.Render([false, .. Enumerable.Repeat(true, head.Count - 1)], rows);
    }

    private static IReadOnlyList<string> Cells(string name, UsageRow row) =>
        [name, UsageUnits.Tokens(row.Tokens), UsageUnits.Usd(row.CostUsd)];
}
