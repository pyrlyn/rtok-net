using Pyrlyn.FsKit;
using Rtok.Config;
using Rtok.Core;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary><c>rtok report</c>: the flags as config keys, the document from the store, and the
/// renderer the merged <c>[report]</c> section asks for.</summary>
public static class ReportPage
{
    /// <summary>The command's flags as <c>[report]</c> keys: a given flag beats the config.</summary>
    public static IReadOnlyList<ConfigFlag> Flags(
        string? format,
        string? @out,
        string? since,
        bool ai
    )
    {
        var flags = new List<ConfigFlag>();
        if (format is not null)
        {
            flags.Add(new ConfigFlag("report.format", format, "--format"));
        }
        if (@out is not null)
        {
            flags.Add(new ConfigFlag("report.out", @out, "--out"));
        }
        if (since is not null)
        {
            flags.Add(new ConfigFlag("report.since", since, "--since"));
        }
        if (ai)
        {
            flags.Add(new ConfigFlag("report.ai", true, "--ai"));
        }
        return flags;
    }

    /// <summary>The report of the window ending at <paramref name="now"/>.</summary>
    /// <param name="loaded">The loaded config, flags merged.</param>
    /// <param name="now">The window's end.</param>
    /// <param name="doctor">The doctor page's text.</param>
    public static ReportDocument Build(LoadedConfig loaded, DateTimeOffset now, string doctor)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        var config = loaded.Config;
        var since = StatsPage.ParseSince(config.Report.Since, "report.since");
        var window = new ReportWindow(now - since, now, config.Report.Since, config.Core.DbPath);
        ReportFacts facts;
        using (
            var store = RtokStore.Open(
                new StoreOptions(config.Core.DbPath) { ArchiveDir = config.Core.ArchiveDir }
            )
        )
        {
            facts = ReportFacts.Read(store, window, AdviceSettings.Of(config));
        }
        return ReportBuilder.Build(
            facts,
            [
                .. loaded.Entries.Select(e => new ConfigLine(
                    e.Key,
                    ConfigDisplay.Show(e.Key, e.Value),
                    ConfigDisplay.Source(e.Source)
                )),
            ],
            doctor
        );
    }

    /// <summary>The renderer <c>[report]</c> asks for: <c>ai</c> wins over <c>format</c>.</summary>
    /// <param name="config">The config, flags merged.</param>
    /// <param name="pdf">The pdf renderer, which lives outside this project.</param>
    public static IReportRenderer Renderer(RtokConfig config, Func<IReportRenderer> pdf)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(pdf);
        var report = config.Report;
        if (report.Ai)
        {
            var e = config.Estimator;
            return new AiRenderer(
                report.BudgetTokens,
                new EstimatorRates((float)e.Code, (float)e.Prose, (float)e.Json, (float)e.Cjk)
            );
        }
        return report.Format switch
        {
            "md" => new MarkdownRenderer(),
            "html" => new HtmlRenderer(),
            "pdf" => pdf(),
            var other => throw new InvalidOperationException(
                $"report.format: {other} is not md, html or pdf"
            ),
        };
    }

    /// <summary>Renders <paramref name="document"/> to <c>[report] out</c>, or to
    /// <paramref name="stdout"/> when it is empty; returns the line to print after a file
    /// write, else null.</summary>
    public static string? Write(
        ReportDocument document,
        IReportRenderer renderer,
        string @out,
        Stream stdout
    )
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(@out);
        if (@out.Length == 0)
        {
            renderer.Render(document, stdout);
            return null;
        }
        AtomicFile.WriteWith(@out, s => renderer.Render(document, s));
        return @out + "\n";
    }
}
