using Rtok.Store.Model;

namespace Rtok.Store.Tests;

public sealed class DocsTests
{
    private static readonly DocEntry[] Serde =
    [
        new("serde::Serialize", "trait", "Serialize a data structure from Rust to a data format."),
        new(
            "serde::Deserialize",
            "trait",
            "Deserialize a data structure from a data format into Rust."
        ),
        new("serde::de::Visitor", "trait", "Walks a Deserialize input one value at a time."),
    ];

    private static DocCrate Crate(string sha = "a") =>
        new()
        {
            Name = "serde",
            Version = "1.0.210",
            Sha256 = sha,
            FormatVersion = 1,
        };

    [Fact]
    public void A_search_ranks_by_bm25_within_one_package()
    {
        using var h = new StoreHarness();
        var store = h.Store;
        store.ReplaceDocCrate(Crate(), Serde);
        store.ReplaceDocCrate(
            new DocCrate
            {
                Name = "other",
                Version = "1",
                Sha256 = "b",
            },
            [new("other::Deserialize", "fn", "Deserialize Deserialize Deserialize")]
        );

        var hits = store.SearchDocItems("serde", "1.0.210", "Deserialize", 5);

        Assert.Equal(
            ["serde::Deserialize", "serde::de::Visitor"],
            hits.Select(i => i.Path),
            StringComparer.Ordinal
        );
        Assert.True(store.DocCrateCached("serde", "1.0.210"));
        Assert.False(store.DocCrateCached("serde", "2"));
        Assert.Equal("trait", store.GetDocItem(hits[0].Id)?.Kind);
        Assert.Null(store.GetDocItem(-1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"")]
    [InlineData("path: AND OR NOT ( * ^")]
    public void Any_query_text_is_searched_as_words(string query)
    {
        using var h = new StoreHarness();
        h.Store.ReplaceDocCrate(Crate(), Serde);

        _ = h.Store.SearchDocItems("serde", "1.0.210", query, 5);
    }

    [Fact]
    public void An_unchanged_sync_keeps_every_id_and_a_change_replaces_them()
    {
        using var h = new StoreHarness();
        var store = h.Store;
        Assert.Equal(3, store.SyncDocCrate(Crate("a"), Serde));
        var ids = store.SearchDocItems("serde", "1.0.210", "data", 5).Select(i => i.Id).ToList();

        Assert.Null(store.SyncDocCrate(Crate("a"), Serde));
        Assert.Equal(ids, store.SearchDocItems("serde", "1.0.210", "data", 5).Select(i => i.Id));

        Assert.Equal(1, store.SyncDocCrate(Crate("b"), [new("serde::New", "fn", "fresh data")]));
        Assert.Equal(
            ["serde::New"],
            store.SearchDocItems("serde", "1.0.210", "data", 5).Select(i => i.Path),
            StringComparer.Ordinal
        );
        Assert.All(ids, id => Assert.Null(store.GetDocItem(id)));

        store.DeleteDocCrate("serde", "1.0.210");
        Assert.False(store.DocCrateCached("serde", "1.0.210"));
        Assert.Empty(store.SearchDocItems("serde", "1.0.210", "fresh", 5));
    }
}
