using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Doctor;
using Rtok.Report;
using Rtok.Report.Pdf;

namespace Rtok.Cli;

/// <summary><c>rtok report</c>: the ledgers as findings with evidence.</summary>
internal static class ReportCommand
{
    /// <summary>Report savings, calls, cache, expand, config and doctor with the recommendations they support.</summary>
    /// <param name="format">md, html or pdf; [report] format when omitted.</param>
    /// <param name="out">Write to this file instead of stdout.</param>
    /// <param name="since">The window: 30d, 12h; [report] since when omitted.</param>
    /// <param name="ai">The report for a model: TOON tables within [report] budget_tokens.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    public static void Run(
        string? format = null,
        string? @out = null,
        string? since = null,
        bool ai = false,
        string? config = null
    )
    {
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions
            {
                ConfigFile = config,
                Flags = ReportPage.Flags(format, @out, since, ai),
            }
        );
        // The doctor page without its live MCP and health probes: the report reads, it does not dial.
        var doctor = DoctorText.Render(
            DoctorPage.Page(
                new DoctorOptions(
                    HostContexts.Of(loaded, null),
                    loaded.Config,
                    Environment.CurrentDirectory
                )
            )
        );
        var document = ReportPage.Build(loaded, DateTimeOffset.UtcNow, doctor);
        var renderer = ReportPage.Renderer(loaded.Config, () => new PdfRenderer());
        using var stdout = Console.OpenStandardOutput();
        if (ReportPage.Write(document, renderer, loaded.Config.Report.Out, stdout) is { } line)
        {
            Console.Out.Write(line);
        }
    }
}
