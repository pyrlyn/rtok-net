using System.Text;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Report;

/// <summary>
/// <c>rtok report --ai</c>: the report shaped for a model. Tables are TOON, each section opens
/// with its <c>#id</c> anchor and units line, and recommendations are a numbered task list. The
/// whole text — header, store path, <c>dropped:</c> line and anchors — stays within the token
/// budget: whole sections are kept first-fit and the rest named on <c>dropped:</c>; when not
/// even the header fits it degrades to the title and the dropped list, then to nothing.
/// </summary>
/// <param name="budgetTokens">The budget, <c>[report] budget_tokens</c>.</param>
/// <param name="rates">The estimator rates the budget is counted with.</param>
public sealed class AiRenderer(long budgetTokens, EstimatorRates rates) : IReportRenderer
{
    /// <summary>The first line.</summary>
    public const string Head = "rtok-report ai\n";

    /// <inheritdoc />
    public string Format => "ai";

    /// <inheritdoc />
    public void Render(ReportDocument document, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Write(Encoding.UTF8.GetBytes(Text(document)));
    }

    /// <summary>The estimated tokens of <paramref name="text"/>, as the budget counts them.</summary>
    public long Tokens(string text) => TokenEstimator.Estimate(text, TextClass.Prose, rates);

    /// <summary>The text within the budget.</summary>
    public string Text(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var bodies = document.Sections.Select(s => (s.Id, Body: Section(s))).ToList();
        var kept = new bool[bodies.Count];
        for (var i = 0; i < bodies.Count; i++)
        {
            kept[i] = true;
            // Every section not kept yet counts as dropped, so the final header is never longer.
            var pessimistic = Compose(document.Window, bodies, kept, i);
            kept[i] = Tokens(pessimistic) <= budgetTokens;
        }
        var text = Compose(document.Window, bodies, kept, bodies.Count - 1);
        if (Tokens(text) <= budgetTokens)
        {
            return text;
        }
        var minimal = Head + Dropped(bodies.Select(b => b.Id));
        return Tokens(minimal) <= budgetTokens ? minimal : "";
    }

    private static string Compose(
        ReportWindow window,
        List<(string Id, string Body)> bodies,
        bool[] kept,
        int decided
    )
    {
        var sb = new StringBuilder(Head);
        sb.Append("range ")
            .Append(window.FromDate)
            .Append("..")
            .Append(window.ToDate)
            .Append(" window=")
            .Append(Line(window.Since))
            .Append(" store=")
            .Append(Line(window.Store))
            .Append('\n');
        sb.Append(Dropped(bodies.Where((_, i) => i > decided || !kept[i]).Select(b => b.Id)));
        for (var i = 0; i <= decided; i++)
        {
            if (kept[i])
            {
                sb.Append('#').Append(bodies[i].Id).Append('\n').Append(bodies[i].Body);
            }
        }
        return sb.ToString();
    }

    private static string Dropped(IEnumerable<string> ids)
    {
        var list = string.Join(',', ids);
        return "dropped: " + (list.Length == 0 ? "none" : list) + "\n";
    }

    private static string Section(ReportSection section)
    {
        var sb = new StringBuilder(section.Units).Append('\n');
        foreach (var block in section.Blocks)
        {
            switch (block)
            {
                case Paragraph p:
                    sb.Append(Line(p.Plain)).Append('\n');
                    break;
                case Table t:
                    sb.Append(Toon.Table(t));
                    break;
                case CodeBlock c:
                    sb.Append(c.Text.EndsWith('\n') ? c.Text : c.Text + "\n");
                    break;
                case Findings { Items.Count: 0 }:
                    sb.Append("no recommendations.\n");
                    break;
                case Findings f:
                    for (var i = 0; i < f.Items.Count; i++)
                    {
                        var item = f.Items[i];
                        sb.Append(Fmt.Num(i + 1))
                            .Append(". [")
                            .Append(Line(item.Rule))
                            .Append("] ")
                            .Append(Line(item.Text))
                            .Append(" (")
                            .Append(Line(item.Evidence))
                            .Append(")\n");
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    // Data never starts a line of its own: a newline in a store string cannot forge an anchor.
    private static string Line(string text) => text.Replace('\n', ' ').Replace('\r', ' ');
}
