using System.Text;
using Rtok.Archive;
using Rtok.Config;
using Rtok.Store;

namespace Rtok.AotTests;

// Archive and expand on NativeAOT (T9): every archive query runs precompiled, and the
// non-backtracking grep regex is built at run time without reflection emit.
public sealed class ArchiveTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("rtok-aot-archive-");

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public void Expand_runs_on_precompiled_queries()
    {
        var options = new StoreOptions(Path.Combine(_dir.FullName, "rtok.db"))
        {
            ArchiveDir = Path.Combine(_dir.FullName, "archive"),
        };
        var body = Encoding.UTF8.GetBytes("alpha\r\nbeta needle\r\ngamma\r\n");
        using var store = RtokStore.Open(options);
        var id = store.PutArchive("proxy", body, agentId: "sub-1");
        store.PutArchiveDecision("proxy", "tu1", id, "[toon 3 rows]");
        var expander = new Expander(store, new RtokConfig(), Expander.CliSession);

        Assert.Equal(body, expander.Run(new ExpandRequest(id[..8])).Body);
        Assert.Equal(
            "2:beta needle\n",
            Encoding.UTF8.GetString(expander.Run(new ExpandRequest(id, Grep: "need(le)?$")).Body!)
        );
        Assert.Equal(
            "gamma\n",
            Encoding.UTF8.GetString(expander.Run(new ExpandRequest(id, "3")).Body!)
        );
        Assert.False(expander.Run(new ExpandRequest(id, "4-5")).Ok);
        Assert.Equal("[toon 3 rows]", store.LivePointer(id));
        Assert.Equal(
            1L,
            StoreTests.Scalar(
                options,
                "SELECT count(*) FROM measurements WHERE plugin = 'toon' AND kind = 'expand'"
            )
        );
        Assert.Equal(
            1L,
            StoreTests.Scalar(
                options,
                "SELECT count(*) FROM archive_decisions WHERE expanded_ts IS NOT NULL"
            )
        );
        Assert.Equal("sub-1", StoreTests.Scalar(options, "SELECT agent_id FROM archive"));
    }
}
