using System.Text;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugin.Sdk.Tests;

public sealed class MemoryHostTests
{
    // The dotted path a plugin reads its own section by reaches into nested config, and a
    // section the host does not have is the plugin's default, not an error.
    [Fact]
    public void ConfigIsReadByDottedPathAndMissingIsDefault()
    {
        var cx = new Ctx(new MemoryHost("""{"plugins": {"mine": {"enabled": true}}}"""));
        Assert.Equal(
            new Cfg { Enabled = true },
            cx.PluginConfig("mine", TestsJsonContext.Default.Cfg)
        );
        Assert.Equal(new Cfg(), cx.PluginConfig("absent", TestsJsonContext.Default.Cfg));
    }

    [Fact]
    public void UnparsableConfigIsDefault()
    {
        var cx = new Ctx(new MemoryHost("""{"plugins": {"mine": {"enabled": "yes"}}}"""));
        Assert.Equal(new Cfg(), cx.PluginConfig("mine", TestsJsonContext.Default.Cfg));
    }

    // Lossless: the id a plugin quotes gives the bytes back, and the same bytes archive to the
    // same id, so a result shortened twice does not grow two archives.
    [Fact]
    public void AnArchivedBlobComesBackUnderAStableId()
    {
        var host = new MemoryHost();
        var body = "the original output"u8;
        var id = host.PutArchive(body);
        Assert.Equal(id, host.PutArchive(body));
        Assert.Equal(body.ToArray(), host.GetArchive(id));
        Assert.Equal(body.Length, host.ArchiveSize(id));
        Assert.Null(host.GetArchive("mem0"));
    }

    // Two bodies of the same length and first byte are still two bodies.
    [Fact]
    public void DistinctBodiesNeverShareAnId()
    {
        var host = new MemoryHost();
        var ab = host.PutArchive("ab"u8);
        var ac = host.PutArchive("ac"u8);
        Assert.NotEqual(ab, ac, StringComparer.Ordinal);
        Assert.Equal("ab", Encoding.UTF8.GetString(host.GetArchive(ab)!));
        Assert.Equal("ac", Encoding.UTF8.GetString(host.GetArchive(ac)!));
    }

    // First writer wins: the pointer text is frozen so every later turn replays the same bytes
    // and the prompt cache survives.
    [Fact]
    public void AnArchiveDecisionIsFrozenByItsFirstWriter()
    {
        var host = new MemoryHost();
        host.PutArchiveDecision("t1", "mem1", "[archived mem1]");
        host.PutArchiveDecision("t1", "mem2", "[archived mem2]");
        var decision = host.GetArchiveDecision("t1")!;
        Assert.Equal("[archived mem1]", decision.PointerText);
        Assert.False(decision.Expanded);
        Assert.Equal(1, host.MarkExpanded("mem1"));
        Assert.True(host.GetArchiveDecision("t1")!.Expanded);
    }

    [Fact]
    public void EstimateRoundsUpPerClass()
    {
        var host = new MemoryHost();
        Assert.Equal(2, host.Estimate("1234567", TextClass.Code));
        Assert.Equal(3, host.Estimate("123456789", TextClass.Prose));
        Assert.Equal(2, host.Estimate("日本", TextClass.Cjk));
        Assert.Equal(0, host.Estimate("", TextClass.Json));
    }

    [Fact]
    public void RecordAndLogKeepOrder()
    {
        var host = new MemoryHost();
        var cx = new Ctx(host);
        cx.Host.Record(Shrink.Measure("demo", "raw", 400, 40));
        cx.Host.Log("info", "plugin", "demo", "shrunk");
        Assert.Equal(400, Assert.Single(host.Recorded()).BeforeBytes);
        Assert.Equal(["info plugin/demo: shrunk"], host.Logged());
    }

    [Fact]
    public void ArchiveInSessionFindsOnlyTheSameContext()
    {
        var host = new MemoryHost();
        var body = "the original output"u8;
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body));
        Assert.Null(host.ArchiveInSession(sha, "agent-a"));
        var id = host.PutArchiveFor(body, "agent-a");
        Assert.Equal(new ArchiveHit(id, 1), host.ArchiveInSession(sha, "agent-a"));
        Assert.Null(host.ArchiveInSession(sha, "agent-b"));
        Assert.Null(host.ArchiveInSession(sha, null));
    }

    [Fact]
    public void ArchiveFailsMakesEveryWriteThrow()
    {
        var host = new MemoryHost { ArchiveFails = true };

        Assert.Throws<IOException>(() => host.PutArchive("x"u8));
        Assert.Throws<IOException>(() => host.PutArchiveFor("x"u8, null));
    }
}
