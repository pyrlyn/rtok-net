namespace Rtok.Report.Tests;

/// <summary>Hand-built facts for the rule tests: an empty window and the default settings.</summary>
internal static class Facts
{
    public static readonly ReportWindow Window = new(
        DateTimeOffset.FromUnixTimeSeconds(ReportFixture.Now - (30 * 86400)),
        DateTimeOffset.FromUnixTimeSeconds(ReportFixture.Now),
        "30d",
        "/home/u/.rtok/rtok.db"
    );

    public static readonly AdviceSettings Settings = new(0.05, 1_000, 20, 500, 3.0, "full");

    public static ReportFacts Empty => new() { Window = Window, Settings = Settings };

    public static ReportDocument Document(ReportFacts facts) =>
        ReportBuilder.Build(facts, [new ConfigLine("report.format", "md", "default")], "ok\n");
}
