using Rtok.Testing;

namespace Rtok.Measure.Tests;

/// <summary>The transcript walk under <c>rtok stats</c>: links followed, every file counted once.</summary>
public sealed class WalkerTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void A_symlinked_alias_counts_once_and_a_dangling_link_is_skipped()
    {
        var real = _dir.Combine("projects", "real");
        Directory.CreateDirectory(real);
        var session = Path.Combine(real, "s.jsonl");
        File.WriteAllText(
            session,
            """{"type":"assistant","message":{"id":"m1","usage":{"input_tokens":5},"content":[{"type":"tool_use","id":"t1","name":"Read","input":{}}]}}"""
                + "\n"
        );
        try
        {
            Directory.CreateSymbolicLink(_dir.Combine("projects", "alias"), real);
            File.CreateSymbolicLink(Path.Combine(real, "again.jsonl"), session);
            File.CreateSymbolicLink(Path.Combine(real, "dead.jsonl"), _dir.Combine("gone.jsonl"));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Assert.Skip($"no symlinks here: {e.Message}");
        }

        var records = CallRecords.Claude(_dir.Combine("projects"), DateTime.UnixEpoch);
        var report = StatsCollector.Collect(
            _dir.Combine("projects"),
            DateTime.UnixEpoch,
            "",
            Lines.Defaults,
            StatsSignals.None
        );

        var only = Assert.Single(records);
        Assert.Equal("s", only.Session); // the real path, never the alias's
        Assert.Equal((1L, 1L), (report.Sessions, report.Lines));
    }
}
