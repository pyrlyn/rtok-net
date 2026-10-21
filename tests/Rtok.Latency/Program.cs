using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

// Hook latency gate (README.md): p50/p95 per fixture with the resident up, fails over 10 ms p95
// or a median 20 % over baseline.
const double BudgetMs = 10;
const double AllowedRegression = 1.2;

// CI runners are not the machine the baseline was measured on: --budget-only keeps the 10 ms rule.
var update = args is ["--update-baseline"];
var budgetOnly = args is ["--budget-only"];
if (args.Length > 0 && !update && !budgetOnly)
{
    await Console.Error.WriteLineAsync("usage: rtok-latency [--update-baseline | --budget-only]");
    return 2;
}

try
{
    await Cli.Wrap("hyperfine").WithArguments("--version").ExecuteBufferedAsync();
}
catch (System.ComponentModel.Win32Exception)
{
    await Console.Error.WriteLineAsync(
        "hyperfine is not on PATH (https://github.com/sharkdp/hyperfine)"
    );
    return 2;
}

using var box = await HookSandbox.CreateAsync();
using var serve = new CancellationTokenSource();
var resident = Cli.Wrap(box.Rtok)
    .WithArguments(["hook", "--serve"])
    .WithEnvironmentVariables(box.Environment)
    .WithValidation(CommandResultValidation.None)
    .ExecuteAsync(serve.Token);
if (
    !await HookSandbox.UntilAsync(() => File.Exists(box.Endpoint.Address), TimeSpan.FromSeconds(10))
)
{
    await Console.Error.WriteLineAsync("the resident did not start");
    return 1;
}
var baselineFile = Path.Join(RepoPaths.Root, "tests", "Rtok.Latency", "baseline.json");
var baseline = JsonNode.Parse(await File.ReadAllTextAsync(baselineFile))!.AsObject();
var ridBaseline = budgetOnly ? null : baseline[PublishedApps.Rid]?.AsObject();
var measured = new JsonObject();
var failed = false;

Console.WriteLine($"rtok-hook on {PublishedApps.Rid}, resident up: {box.Hook}");
Console.WriteLine($"{"fixture", -20} {"p50 ms", 8} {"p95 ms", 8} {"base ms", 8}  status");
foreach (
    var fixture in Directory
        .EnumerateFiles(RepoPaths.Fixture("hooks"), "*.json")
        .Order(StringComparer.Ordinal)
)
{
    var name = Path.GetFileNameWithoutExtension(fixture);
    var times = await MeasureAsync(box, fixture);
    var p50 = Percentile(times, 0.50);
    var p95 = Percentile(times, 0.95);
    var base50 = ridBaseline?[name]?.GetValue<double>();
    var failure =
        p95 > BudgetMs ? $"FAIL p95 over {BudgetMs} ms"
        : base50 is { } b && p50 > b * AllowedRegression ? "FAIL over 120 % of baseline"
        : null;
    failed |= failure is not null;
    measured[name] = Math.Round(p50, 2);
    Console.WriteLine(
        string.Create(
            CultureInfo.InvariantCulture,
            $"{name, -20} {p50, 8:F2} {p95, 8:F2} {(base50 is { } v ? v.ToString("F2", CultureInfo.InvariantCulture) : "-"), 8}  {failure ?? "ok"}"
        )
    );
}

await serve.CancelAsync();
try
{
    await resident;
}
catch (OperationCanceledException) { }

if (update)
{
    baseline[PublishedApps.Rid] = measured;
    await File.WriteAllTextAsync(
        baselineFile,
        baseline.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n"
    );
    Console.WriteLine($"baseline for {PublishedApps.Rid} written to {baselineFile}");
    return 0;
}

return failed ? 1 : 0;

// Run times in milliseconds of 200 spawns with the fixture on stdin.
static async Task<double[]> MeasureAsync(HookSandbox box, string fixture)
{
    var export = Path.Join(Path.GetTempPath(), $"rtok-latency-{Guid.NewGuid():N}.json");
    try
    {
        await Cli.Wrap("hyperfine")
            .WithEnvironmentVariables(box.Environment)
            .WithArguments([
                "-N",
                "--warmup",
                "20",
                "--runs",
                "200",
                "--style",
                "none",
                "--input",
                fixture,
                "--export-json",
                export,
                $"'{box.Hook}'",
            ])
            .ExecuteBufferedAsync();
        var result = JsonNode.Parse(await File.ReadAllTextAsync(export))!["results"]![0]!;
        return [.. result["times"]!.AsArray().Select(t => t!.GetValue<double>() * 1000)];
    }
    finally
    {
        File.Delete(export);
    }
}

// Nearest-rank percentile.
static double Percentile(double[] values, double p)
{
    var sorted = values.Order().ToArray();
    return sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
}
