using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Smoke;

/// <summary>The NativeAOT binary renders the report's PDF: QuestPDF and its native libraries
/// survive the publish.</summary>
public sealed partial class ReportTests
{
    [Fact]
    public async Task Report_writes_a_pdf_that_opens()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync("rtok", ct);
        using var dir = new TempDir("rtok-smoke-report-");
        var home = dir.Combine("user");
        var config = dir.Combine("config.toml");
        var db = dir.Combine("rtok.db");
        await File.WriteAllTextAsync(
            config,
            $"[core]\ndb_path = '{db}'\narchive_dir = '{dir.Combine("archive")}'\n",
            ct
        );
        using (
            var store = RtokStore.Open(new StoreOptions(db) { ArchiveDir = dir.Combine("archive") })
        )
        {
            store.UpsertSession("s1", store.HostId("claude"), "p", "/p", "claude");
            store.InsertMeasurement(
                "s1",
                new MeasurementInput("cmd", "rule", 4_000, 400, 1_000, 100, "git:a1")
            );
        }
        var pdf = dir.Combine("report.pdf");

        var result = await Cli.Wrap(rtok)
            .WithArguments(["report", "--format", "pdf", "--out", pdf, "--config", config])
            .WithEnvironmentVariables(env =>
                env.Set("HOME", home).Set("RTOK_HOME", Path.Join(home, ".rtok"))
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        Assert.Equal((0, ""), (result.ExitCode, result.StandardError));
        Assert.Equal(pdf + "\n", result.StandardOutput.ReplaceLineEndings("\n"));
        var bytes = await File.ReadAllBytesAsync(pdf, ct);
        var text = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF", text.TrimEnd(), StringComparison.Ordinal);
        Assert.NotEmpty(Page().Matches(text));
    }

    [GeneratedRegex(@"/Type\s*/Page\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Page();
}
