using System.Text;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Measure.Tests;

public sealed class CacheHealthTests
{
    private const string A = """{"system":"s","tools":[{"name":"A"}],"messages":[]}""";
    private const string AMoreTools =
        """{"system":"s","tools":[{"name":"A"},{"name":"B"}],"messages":[]}""";
    private const string ANewSystem = """{"system":"s2","tools":[{"name":"A"}],"messages":[]}""";

    private static UsageTurn Row(long id, long create, long read, string api = "anthropic") =>
        new(id, api, 100, create, read);

    /// <summary>A changed tools array is exactly one bust, its cause <c>tools</c>.</summary>
    [Fact]
    public void A_tools_change_is_one_bust_with_cause_tools()
    {
        using var dir = new TempDir();
        using var store = RtokStore.Open(
            new StoreOptions(dir.Combine("rtok.db")) { ArchiveDir = dir.Combine("archive") }
        );
        store.UpsertSession("sess", null, null, null, "proxy");
        foreach (
            var (body, create, read) in new[]
            {
                (A, 30_000L, 0L),
                (A, 500L, 30_000L),
                (AMoreTools, 31_000L, 200L),
                (AMoreTools, 400L, 31_000L),
            }
        )
        {
            var id = store.InsertCall("sess", "proxy", "api_request");
            store.InsertCallIo(id, Encoding.UTF8.GetBytes(body), null);
            store.InsertUsage("sess", "m", "anthropic", 100, create, read, 5, id);
        }

        var r = CacheHealth.Report(store);

        var s = Assert.Single(r);
        Assert.Equal((1, 4), (s.Busts, s.Turns.Count));
        var busts = s.Turns.Select((t, i) => (Turn: i + 1, t.Bust)).Where(x => x.Bust is not null);
        Assert.Equal([(3, "tools")], busts);
        Assert.Contains(
            "bust turn 3 cause=tools cache_create=31000",
            CacheHealth.Table(r),
            StringComparison.Ordinal
        );
        Assert.Contains("\"bust\": \"tools\"", CacheHealth.Json(r), StringComparison.Ordinal);
    }

    [Fact]
    public void A_system_change_an_unknown_body_and_a_growing_prefix()
    {
        UsageTurn[] rows = [Row(1, 0, 0), Row(2, 500, 40_000), Row(3, 25_000, 1_000)];
        string[] bodies = [A, A, ANewSystem];
        var turns = CacheHealth.Analyse(rows, id => Encoding.UTF8.GetBytes(bodies[id - 1]));
        Assert.Equal([null, null, "system"], turns.Select(t => t.Bust), StringComparer.Ordinal);
        // No recorded body: unknown.
        Assert.Equal("unknown", CacheHealth.Analyse(rows, _ => null)[2].Bust);
        // A big write without a read drop is a growing prefix, not a bust.
        UsageTurn[] growing = [Row(1, 0, 0), Row(2, 500, 40_000), Row(3, 25_000, 45_000)];
        Assert.All(CacheHealth.Analyse(growing, _ => null), t => Assert.Null(t.Bust));
        Assert.Contains("no usage rows", CacheHealth.Table([]), StringComparison.Ordinal);
    }

    [Fact]
    public void An_openai_read_drop_is_a_bust_without_a_write()
    {
        var openai = CacheHealth.Analyse(
            [Row(1, 0, 100, "openai_chat"), Row(1, 0, 10, "openai_chat")],
            _ => null
        );
        Assert.Equal([null, "unknown"], openai.Select(t => t.Bust), StringComparer.Ordinal);
        Assert.All(
            CacheHealth.Analyse([Row(1, 0, 100), Row(2, 0, 10)], _ => null),
            t => Assert.Null(t.Bust)
        );
    }

    [Fact]
    public void A_body_that_is_not_json_has_no_shape()
    {
        UsageTurn[] rows = [Row(1, 0, 40_000), Row(2, 25_000, 0)];
        Assert.Equal("unknown", CacheHealth.Analyse(rows, _ => "not json"u8.ToArray())[1].Bust);
    }
}
