using Rtok.Testing;

namespace Rtok.Cli.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void Only_Rtok_Process_may_start_processes()
    {
        var banned = File.ReadAllLines(
            Path.Join(RepoPaths.Root, "eng", "BannedSymbols.Process.txt")
        );
        Assert.Contains(
            banned,
            l => l.StartsWith("M:System.Diagnostics.Process.Start;", StringComparison.Ordinal)
        );
        Assert.Contains(
            banned,
            l =>
                l.StartsWith(
                    "M:System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo);",
                    StringComparison.Ordinal
                )
        );

        var exempt = Directory
            .EnumerateFiles(RepoPaths.Root, "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(RepoPaths.Root, p).Replace('\\', '/'))
            .Where(p =>
                !p.StartsWith("artifacts/", StringComparison.Ordinal)
                && !p.StartsWith(".worktrees/", StringComparison.Ordinal)
                && !p.Contains("/tests/", StringComparison.Ordinal)
                && !p.StartsWith("tests/", StringComparison.Ordinal)
                && File.ReadAllText(Path.Join(RepoPaths.Root, p))
                    .Contains("<RtokProcessRunner>true", StringComparison.Ordinal)
            );

        Assert.Equal("src/Rtok.Process/Rtok.Process.csproj", Assert.Single(exempt));
    }
}
