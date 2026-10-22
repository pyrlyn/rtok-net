using Microsoft.Data.Sqlite;
using Rtok.Plugin.Sdk;

namespace Rtok.Store.Tests;

public sealed class SymbolsTests
{
    private static SymbolRow Row(string name, int line, bool isDef) =>
        new(name, "function", line, isDef, line, "");

    private static SymbolRow Import(string name, int line, string spec = "") =>
        new(name, "import", line, false, line, spec);

    private static SymbolRow Ref(string name, int line, string scope) =>
        new(name, "function", line, false, line, scope);

    private static void Put(RtokStore store, string root, string path, params SymbolRow[] rows) =>
        store.ReplaceSymbols(root, path, "s", 0, 0, rows);

    [Fact]
    public void A_replace_swaps_one_files_rows_and_keeps_its_stat()
    {
        using var h = new StoreHarness();
        var s = h.Store;
        Assert.Equal(
            2,
            s.ReplaceSymbols("/r", "a.rs", "sha1", 10, 20, [Row("f", 1, true), Row("g", 2, false)])
        );
        Assert.Equal(1, s.ReplaceSymbols("/r", "a.rs", "sha2", 11, 21, [Row("h", 1, true)]));
        Assert.Equal(1, s.SymbolCount("/r"));
        Assert.Equal(new SymbolStat("sha2", 11, 21), s.GetSymbolStat("/r", "a.rs"));
        Assert.Null(s.GetSymbolStat("/r", "b.rs"));

        s.TouchSymbols("/r", "a.rs", 12, 22);
        Assert.Equal(new SymbolStat("sha2", 12, 22), s.SymbolStats("/r")["a.rs"]);
    }

    // Inside a store batch (T13) the raw symbol commands join the batch's transaction.
    [Fact]
    public void Symbol_writes_and_reads_join_an_open_batch()
    {
        using var h = new StoreHarness();
        using (var batch = h.Store.BeginBatch())
        {
            Put(h.Store, "/r", "a.rs", Row("f", 1, true));
            Assert.Equal(
                ["a.rs"],
                h.Store.SymbolDefs("/r", "f").Select(d => d.Path),
                StringComparer.Ordinal
            );
            batch.Commit();
        }

        Assert.Equal(1, h.Store.SymbolCount("/r"));
    }

    [Fact]
    public void A_tagless_file_keeps_one_nameless_row_for_its_sha()
    {
        using var h = new StoreHarness();
        Assert.Equal(0, h.Store.ReplaceSymbols("/r", "empty.rs", "sha", 5, 6, []));
        Assert.Equal(new SymbolStat("sha", 5, 6), h.Store.GetSymbolStat("/r", "empty.rs"));
        Assert.Equal(1, h.Store.SymbolFileCount("/r"));
        Assert.Empty(h.Store.SymbolFileScan("/r"));
    }

    [Fact]
    public void A_batch_writes_every_file_in_one_call()
    {
        using var h = new StoreHarness();
        var inserted = h.Store.ReplaceSymbolFiles(
            "/r",
            [
                new SymbolFile("a.rs", "1", 1, 1, [Row("a", 1, true)]),
                new SymbolFile("b.rs", "2", 2, 2, [Row("b", 1, true), Row("a", 2, false)]),
            ]
        );
        Assert.Equal(3, inserted);
        Assert.Equal(
            ["a.rs", "b.rs"],
            h.Store.SymbolStats("/r").Keys.Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        Assert.Equal([("b.rs", "", 1L, 2)], h.Store.SymbolRefGroups("/r", "a"));
    }

    [Fact]
    public void Two_roots_never_see_each_others_rows()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/one", "a.rs", Row("f", 1, true));
        Put(h.Store, "/two", "a.rs", Row("f", 3, true));
        Assert.Equal([("a.rs", "function", 1, 1)], h.Store.SymbolDefs("/one", "f"));
        Assert.Equal(
            1,
            h.Store.DeleteSymbolsMissing("/one", new HashSet<string>(StringComparer.Ordinal))
        );
        Assert.Equal(0, h.Store.SymbolCount("/one"));
        Assert.Equal(1, h.Store.SymbolCount("/two"));
    }

    [Fact]
    public void Delete_missing_drops_only_the_files_not_kept()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r", "a.rs", Row("a", 1, true));
        Put(h.Store, "/r", "b.rs", Row("b", 1, true), Row("c", 2, true));
        Assert.Equal(
            2,
            h.Store.DeleteSymbolsMissing(
                "/r",
                new HashSet<string>(StringComparer.Ordinal) { "a.rs" }
            )
        );
        Assert.Equal("a.rs", Assert.Single(h.Store.SymbolStats("/r").Keys));
        Assert.Equal(
            0,
            h.Store.DeleteSymbolsMissing(
                "/r",
                new HashSet<string>(StringComparer.Ordinal) { "a.rs" }
            )
        );
    }

    [Fact]
    public void A_stale_mark_drops_the_rows_under_their_root_and_shows_as_pending()
    {
        using var h = new StoreHarness();
        var root = h.Combine("repo");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        var file = Path.Combine(root, "src", "a.rs");
        File.WriteAllText(file, "fn a() {}\n");
        var (mtime, size) = SymbolStat.Stamp(file);
        h.Store.ReplaceSymbols(root, "src/a.rs", "s", mtime, size, [Row("a", 1, true)]);
        h.Store.ReplaceSymbols("/elsewhere", "src/a.rs", "s", 0, 0, [Row("a", 1, true)]);
        Assert.Empty(h.Store.SymbolPending(root, root));

        h.Store.MarkSymbolsStale(file);
        Assert.Equal(0, h.Store.SymbolCount(root));
        Assert.Equal(1, h.Store.SymbolCount("/elsewhere"));
        Assert.Equal(["src/a.rs"], h.Store.SymbolPending(root, root));

        h.Store.ReplaceSymbols(root, "src/a.rs", "s", mtime, size, [Row("a", 1, true)]);
        Assert.Empty(h.Store.SymbolPending(root, root));
        File.WriteAllText(file, "fn a() {}\nfn b() {}\n");
        Assert.Equal(["src/a.rs"], h.Store.SymbolPending(root, root));
    }

    [Fact]
    public void The_stamp_of_a_missing_file_is_zero()
    {
        using var h = new StoreHarness();
        Assert.Equal((0L, 0L), SymbolStat.Stamp(h.Combine("missing.rs")));
    }

    [Fact]
    public void Fingerprint_index_time_and_file_rank_are_kept_per_root()
    {
        using var h = new StoreHarness();
        var s = h.Store;
        Assert.Null(s.ExtractorFingerprint("/r"));
        Assert.Null(s.SymbolIndexedAt("/r"));
        s.TouchSymbolIndexedAt("/r", 100);
        Assert.Equal("", s.ExtractorFingerprint("/r"));
        s.SetExtractorFingerprint("/r", "fp");
        s.TouchSymbolIndexedAt("/r", 200);
        Assert.Equal(("fp", 200L), (s.ExtractorFingerprint("/r"), s.SymbolIndexedAt("/r")));
        Assert.Null(s.ExtractorFingerprint("/other"));

        Assert.Null(s.FileRankGet("/r"));
        s.FileRankPut("/r", "{}");
        s.FileRankPut("/r", "{\"a\":1}");
        Assert.Equal("{\"a\":1}", s.FileRankGet("/r"));
    }

    [Fact]
    public void Top_refs_rank_by_count_then_name()
    {
        using var h = new StoreHarness();
        Put(
            h.Store,
            "/r",
            "a.rs",
            Row("foo", 1, true),
            Row("bar", 2, true),
            Row("aaa", 3, true),
            Row("zed", 4, true),
            Row("foo", 10, false),
            Row("foo", 11, false),
            Row("bar", 12, false),
            Row("bar", 13, false),
            Row("aaa", 14, false),
            Import("foo", 20)
        );
        var got = h.Store.SymbolTopRefs("/r", 10);
        Assert.Equal(
            [("bar", 2L), ("foo", 2L), ("aaa", 1L), ("zed", 0L)],
            got.Select(r => (r.Name, r.Refs))
        );
        Assert.Equal(got, h.Store.SymbolTopRefs("/r", 10));
    }

    [Fact]
    public void Top_refs_pick_the_first_definition_site()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r", "b.rs", Row("dup", 5, true));
        Put(h.Store, "/r", "a.rs", Row("dup", 9, true), Row("dup", 3, true), Row("dup", 1, false));
        Assert.Equal([("dup", 1L, "a.rs", 3)], h.Store.SymbolTopRefs("/r", 4));
    }

    [Fact]
    public void Impact_excludes_a_reference_name_already_on_the_chain()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r1", "a.rs", Row("X", 1, true), Ref("N", 2, "X"));
        Put(h.Store, "/r1", "b.rs", Row("X", 1, true), Ref("X", 2, "X"));
        Assert.Equal([(1, "a.rs", "X")], h.Store.SymbolImpact("/r1", "N", 4));
    }

    [Fact]
    public void Impact_excludes_an_import_follow_name_already_on_the_chain()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r2", "c.rs", Import("N2", 1), Row("M", 2, true));
        Put(h.Store, "/r2", "e.rs", Import("M", 1), Row("M", 2, true));
        Assert.Equal([(1, "c.rs", "M")], h.Store.SymbolImpact("/r2", "N2", 4));
    }

    [Fact]
    public void Impact_records_an_empty_out_name_but_does_not_follow_it()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r3", "a.rs", Row("N3", 2, false));
        Put(h.Store, "/r3", "b.rs", Import("", 1), Row("LEAK", 2, true));
        Assert.Equal([(1, "a.rs", "")], h.Store.SymbolImpact("/r3", "N3", 4));
    }

    [Fact]
    public void Impact_reaches_callers_of_callers_at_their_first_depth()
    {
        using var h = new StoreHarness();
        Put(
            h.Store,
            "/r",
            "a.rs",
            Row("leaf", 1, true),
            Row("mid", 2, true),
            Ref("leaf", 3, "mid")
        );
        Put(
            h.Store,
            "/r",
            "b.rs",
            Row("top", 1, true),
            Ref("mid", 2, "top"),
            Ref("leaf", 3, "top")
        );
        Put(h.Store, "/r", "c.rs", Row("app", 1, true), Ref("top", 2, "app"));
        Assert.Equal(
            [(1, "a.rs", "mid"), (1, "b.rs", "top"), (2, "c.rs", "app")],
            h.Store.SymbolImpact("/r", "leaf", 4)
        );
        Assert.Equal(
            [(1, "a.rs", "mid"), (1, "b.rs", "top")],
            h.Store.SymbolImpact("/r", "leaf", 1)
        );
    }

    [Fact]
    public void Paths_list_the_shortest_chains_first_and_stop_at_the_target()
    {
        using var h = new StoreHarness();
        Put(
            h.Store,
            "/r",
            "a.rs",
            Row("leaf", 1, true),
            Row("mid", 2, true),
            Ref("leaf", 3, "mid")
        );
        Put(
            h.Store,
            "/r",
            "b.rs",
            Row("top", 1, true),
            Ref("mid", 2, "top"),
            Ref("leaf", 3, "top")
        );
        Assert.Equal(
            ["leaf → top", "leaf → mid → top"],
            h.Store.SymbolPaths("/r", "leaf", "top", 4)
        );
        Assert.Equal(["leaf → top"], h.Store.SymbolPaths("/r", "leaf", "top", 2));
        Assert.Equal(["leaf"], h.Store.SymbolPaths("/r", "leaf", "leaf", 4));
        Assert.Empty(h.Store.SymbolPaths("/r", "top", "leaf", 4));
    }

    [Fact]
    public void Callees_dead_candidates_and_referenced_names_follow_the_rows()
    {
        using var h = new StoreHarness();
        Put(
            h.Store,
            "/r",
            "a.rs",
            Row("main", 1, true),
            Ref("run", 2, "main"),
            Ref("run", 3, "main"),
            Ref("log", 4, "main"),
            Row("run", 6, true),
            Row("unused", 8, true),
            Import("unused", 9)
        );
        Assert.Equal(
            [("a.rs", 1, "run", 2), ("a.rs", 1, "log", 4)],
            h.Store.SymbolCallees("/r", "main")
        );
        Assert.Equal(
            [("a.rs", "main", "function", 1), ("a.rs", "unused", "function", 8)],
            h.Store.SymbolDeadCandidates("/r")
        );
        Assert.Equal(
            "run",
            Assert.Single(h.Store.SymbolReferencedNames("/r", ["run", "unused", "main"]))
        );
        Assert.Equal(["run"], h.Store.SymbolNamePrefix("/r", "r", 5));
        Assert.Equal(
            ["main", "run", "unused"],
            h.Store.SymbolNamePrefix("/r", "", 5).Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        Assert.Empty(h.Store.SymbolNamePrefix("/r", "%", 5));
        Assert.Equal(
            [("main", "function", 1, 1), ("run", "function", 6, 6), ("unused", "function", 8, 8)],
            h.Store.SymbolFileDefs("/r", "a.rs")
        );
        Assert.Equal([("unused", 9)], h.Store.SymbolImports("/r", "a.rs"));
        Assert.Equal([("a.rs", 9)], h.Store.SymbolImporters("/r", "unused"));
        Assert.Equal(
            [("a.rs", "main"), ("a.rs", "run"), ("a.rs", "unused")],
            h.Store.SymbolImportFollow("/r", "unused")
        );
    }

    [Theory]
    [InlineData("crate::a::parse", "src/a.rs", true)]
    [InlineData("crate::plugins::toon::encode", "src/plugins/toon/mod.rs", true)]
    [InlineData("rtok_sys::unlock", "crates/rtok-sys/src/lib.rs", true)]
    [InlineData("rtok_mcp::spec::Format", "crates/rtok-mcp/src/spec.rs", true)]
    [InlineData("crate::a::parse", "src/b.rs", false)]
    [InlineData("figment::providers::Format", "src/doctor/hooks.rs", false)]
    [InlineData("parse", "src/a.rs", false)]
    public void An_import_spec_matches_the_defining_file(string spec, string file, bool matches) =>
        Assert.Equal(matches, ImportKeys.ImportMatchesFile(spec, file));

    [Fact]
    public void Imported_defs_are_the_candidate_files_a_reference_imports()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r", "src/a.rs", Row("parse", 1, true));
        Put(h.Store, "/r", "src/b.rs", Row("parse", 1, true));
        Put(
            h.Store,
            "/r",
            "src/c.rs",
            Import("parse", 1, "crate::a::parse"),
            Ref("parse", 3, "go")
        );
        Assert.Equal([("src/c.rs", "src/a.rs")], h.Store.SymbolImportedDefs("/r", "parse"));
        Assert.Equal((0L, 0L), h.Store.SymbolNameFreq("/r", "parse"));
        h.Store.RebuildSymbolIdf("/r");
        Assert.Equal((1L, 3L), h.Store.SymbolNameFreq("/r", "parse"));
        Assert.Equal((0L, 3L), h.Store.SymbolNameFreq("/r", "missing"));
    }

    [Fact]
    public void Fts_ranks_a_name_token_above_a_signature_and_a_doc_hit()
    {
        using var h = new StoreHarness();
        Put(
            h.Store,
            "/r",
            "a.rs",
            Documented("body_lines", 1, "fn body_lines() {}", "Source of one definition"),
            Documented("mid", 2, "fn mid(source: Lines)", ""),
            Documented("other", 3, "fn other()", "truncated source lines in the comment")
        );
        Assert.Empty(h.Store.SymbolFts("/r", "", 10));
        Assert.Equal(
            ["body_lines", "mid", "other"],
            h.Store.SymbolFts("/r", "truncated source lines", 10).Select(x => x.Name),
            StringComparer.Ordinal
        );
        Assert.Empty(h.Store.SymbolFts("/other", "source", 10));
    }

    [Fact]
    public void A_span_comes_back_for_the_indexed_definition()
    {
        using var h = new StoreHarness();
        var def = Row("f", 2, true) with { StartByte = 4, EndByte = 9, ContentHash = "h" };
        h.Store.ReplaceSymbols("/r", "a.rs", "sha", 0, 0, [def]);
        Assert.Equal(
            new SymbolSpan(4, 9, "h", "sha"),
            h.Store.GetSymbolSpan("/r", "a.rs", "f", "function", 2)
        );
        Assert.Null(h.Store.GetSymbolSpan("/r", "a.rs", "f", "function", 3));
    }

    [Fact]
    public void Rebuilding_the_idf_leaves_planner_statistics_for_both_indexes()
    {
        using var h = new StoreHarness();
        Put(h.Store, "/r", "a.rs", Row("parse", 1, true));
        Put(h.Store, "/r", "b.rs", Row("emit", 1, true));
        h.Store.RebuildSymbolIdf("/r");
        using var connection = new SqliteConnection(
            $"Data Source={h.Options.DbPath};Pooling=False"
        );
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT idx FROM sqlite_stat1 WHERE tbl = 'symbols'";
        var idx = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                idx.Add(reader.GetString(0));
            }
        }

        Assert.Contains(idx, i => i is "IX_symbols_root_name");
        Assert.Contains(idx, i => i is "IX_symbols_root_path");
    }

    [Fact]
    public void The_name_frequency_follows_a_rebuild_through_another_handle()
    {
        using var h = new StoreHarness();
        using var writer = RtokStore.Open(h.Options);
        var reader = h.Store;
        writer.ReplaceSymbols("/r", "a.rs", "s", 0, 0, [Row("parse", 1, false)]);
        writer.RebuildSymbolIdf("/r");
        Assert.Equal((1L, 1L), reader.SymbolNameFreq("/r", "parse"));
        Assert.Equal((0L, 1L), reader.SymbolNameFreq("/r", "emit"));
        writer.ReplaceSymbols("/r", "b.rs", "s", 0, 0, [Row("parse", 1, false)]);
        writer.RebuildSymbolIdf("/r");
        Assert.Equal((2L, 2L), reader.SymbolNameFreq("/r", "parse"));
        Assert.Equal((0L, 0L), reader.SymbolNameFreq("/other", "parse"));
    }

    private static SymbolRow Documented(string name, int line, string signature, string doc) =>
        new SymbolRow(name, "function", line, true, line, "") { Signature = signature, Doc = doc };
}
