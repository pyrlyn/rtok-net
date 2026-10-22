using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk;
using Rtok.Testing;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/index.rs: the stat gate, the sha gate, the fingerprint, the workers and the rows.
public sealed class IndexerTests
{
    private static void Later(string file) =>
        File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(5));

    [Fact]
    public void Home_and_the_filesystem_root_are_refused_but_a_project_is_indexed()
    {
        using var h = new GraphHarness();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var root in (string[])["/", home])
        {
            var e = Assert.Throws<GraphRootException>(() => Indexer.Run(h.Cx, root));
            Assert.Contains("no project root", e.Message, StringComparison.Ordinal);
            Assert.Throws<GraphRootException>(() => Indexer.RunChanged(h.Cx, root, []));
            Assert.Throws<GraphRootException>(() => Indexer.Ensure(h.Cx, root));
            Assert.Equal(0, h.Store.SymbolCount(root));
        }

        h.Write("a.rs", "pub fn alpha() {}\n");
        Assert.Equal(1, h.Run().Indexed);
    }

    [Fact]
    public void Indexing_fills_definitions_and_references_and_skips_gitignored_files()
    {
        using var h = new GraphHarness();
        Directory.CreateDirectory(h.Project.Combine(".git"));
        h.Write(".gitignore", "target/\n");
        h.Write("target/gen.rs", "fn generated() {}\n");
        h.Write(
            "src/lib.rs",
            "struct Foo;\nimpl Foo { fn new() -> Foo { Foo } }\nfn main() { let f: Foo = Foo::new(); helper(); }\nfn helper() {}\n"
        );

        h.Run();

        Assert.True(h.HasDef("main"));
        Assert.True(h.HasDef("helper"));
        Assert.False(h.HasDef("generated"));
        Assert.Contains(h.Store.SymbolRefGroups(h.Root, "helper"), g => g.Path is "src/lib.rs");
        // Type-position and scoped-call references both land on the type's name.
        Assert.True(h.Store.SymbolRefGroups(h.Root, "Foo").Sum(g => g.Count) >= 2);
        Assert.Contains(h.Store.SymbolRefGroups(h.Root, "new"), g => g.Path is "src/lib.rs");
    }

    [Fact]
    public void Two_roots_do_not_evict_each_other()
    {
        using var h = new GraphHarness();
        using var b = new TempDir("rtok-graph-b-");
        h.Write("src/main.rs", "fn main() {}\nfn alpha() {}\n");
        b.Write("src/main.rs", "fn main() {}\nfn beta() {}\n");
        h.Run();
        var aRows = h.Store.SymbolCount(h.Root);
        Indexer.Run(h.Cx, b.Path);
        var kb = GraphRoot.Check(b.Path);

        Assert.Equal(aRows, h.Store.SymbolCount(h.Root));
        Assert.Single(h.Store.SymbolDefs(h.Root, "main"));
        Assert.False(h.HasDef("beta"));
        h.Store.MarkSymbolsStale(Path.Join(h.Root, "src/main.rs"));
        Assert.False(h.HasDef("alpha"));
        Assert.NotEmpty(h.Store.SymbolDefs(kb, "beta"));
    }

    [Fact]
    public void A_root_reached_through_a_symlink_shares_the_canonical_index()
    {
        using var h = new GraphHarness();
        using var links = new TempDir("rtok-graph-link-");
        h.Write("a.rs", "fn alpha() {}\n");
        var link = links.Combine("project");
        Directory.CreateSymbolicLink(link, h.Project.Path);

        Assert.Equal(1, Indexer.Run(h.Cx, link).Indexed);
        Assert.Equal(GraphRoot.Check(link), h.Root);
        Assert.True(h.HasDef("alpha"));
        Assert.Equal(0, h.Run().Read);
    }

    [Fact]
    public void A_fingerprint_mismatch_reindexes_cold()
    {
        using var h = new GraphHarness();
        for (var i = 0; i < 3; i++)
        {
            h.Write($"f{i}.rs", $"fn f{i}() {{}}\n");
        }

        Assert.Equal(3, h.Run().Read);
        Assert.Equal(0, h.Run().Read);
        h.Store.SetExtractorFingerprint(h.Root, "deadbeef");
        var again = h.Run();
        Assert.Equal((3, 3), (again.Read, again.Indexed));
        Assert.Equal(0, h.Run().Read);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remapping_an_extension_drops_the_old_grammars_rows(bool touch)
    {
        using var h = new GraphHarness("""{"extensions":{"tpl":"rust"}}""");
        var file = h.Write("sample.tpl", "fn old() {}\n");
        h.Run();
        Assert.True(h.HasDef("old"));
        Assert.Equal(0, h.Run().Read);

        h.GraphJson = """{"extensions":{"tpl":"py"}}""";
        if (touch)
        {
            Later(file);
        }

        h.Run();
        Assert.False(h.HasDef("old"));
        Assert.Equal(0, h.Run().Read);
    }

    [Fact]
    public void The_fingerprint_follows_the_mapping_content_not_its_order()
    {
        static Dictionary<string, string> Map(params (string, string)[] pairs) =>
            pairs.ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

        var ab = Extraction.Fingerprint(Map(("a", "rust"), ("b", "py")));
        Assert.Equal(ab, Extraction.Fingerprint(Map(("b", "py"), ("a", "rust"))));
        Assert.NotEqual(
            ab,
            Extraction.Fingerprint(Map(("a", "rust"), ("b", "go"))),
            StringComparer.Ordinal
        );
        Assert.NotEqual(ab, Extraction.Fingerprint(Map()), StringComparer.Ordinal);
    }

    [Fact]
    public void An_event_after_a_remap_reparses_the_file()
    {
        using var h = new GraphHarness("""{"extensions":{"tpl":"rust"}}""");
        var file = h.Write("sample.tpl", "fn old() {}\n");
        h.Run();
        h.GraphJson = """{"extensions":{"tpl":"py"}}""";
        Indexer.RunChanged(h.Cx, h.Project.Path, [file]);
        Assert.False(h.HasDef("old"));
    }

    [Fact]
    public void A_stale_fingerprint_dry_run_predicts_the_cold_reindex_and_writes_nothing()
    {
        using var h = new GraphHarness();
        for (var i = 0; i < 3; i++)
        {
            h.Write($"f{i}.rs", $"fn f{i}() {{}}\n");
        }

        h.Run();
        Assert.Equal(0, h.Run(dryRun: true).Indexed);
        h.Store.SetExtractorFingerprint(h.Root, "deadbeef");
        (long, long?, string?) Seen() =>
            (
                h.Store.SymbolCount(h.Root),
                h.Store.SymbolIndexedAt(h.Root),
                h.Store.ExtractorFingerprint(h.Root)
            );
        var before = Seen();
        var predicted = h.Run(dryRun: true);
        Assert.Equal(before, Seen());
        var actual = h.Run();
        Assert.Equal(3, predicted.Indexed);
        Assert.Equal(
            (predicted.Indexed, predicted.Inserted, predicted.Read),
            (actual.Indexed, actual.Inserted, actual.Read)
        );
    }

    [Fact]
    public void A_warm_run_reads_nothing_and_identical_bytes_insert_nothing()
    {
        const int Files = 20;
        using var h = new GraphHarness();
        for (var i = 0; i < Files; i++)
        {
            h.Write($"f{i}.rs", $"fn f{i}() {{}}\n");
        }

        Assert.Equal(Files, h.Run().Read);
        var warm = h.Run();
        Assert.Equal((0, Files, 0), (warm.Read, warm.Skipped, warm.Inserted));
        Later(h.Write("f0.rs", "fn f0() {}\n"));
        var touched = h.Run();
        Assert.Equal((1, 0), (touched.Read, touched.Inserted));
        Assert.Equal(0, h.Run().Read);
    }

    [Fact]
    public void A_latin1_file_records_the_empty_sha_and_is_not_reread()
    {
        using var h = new GraphHarness();
        File.WriteAllBytes(h.Project.Combine("bad.rs"), [.. "fn latin() {}\n"u8, 0xE9]);
        h.Run();
        Assert.Equal(0, h.Run().Read);
        Assert.Equal(Indexer.EmptySha, h.Store.GetSymbolStat(h.Root, "bad.rs")?.Sha);
    }

    [Fact]
    public void A_file_unreadable_at_index_time_is_stored_with_no_stat_and_read_once_access_returns()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var h = new GraphHarness();
        var file = h.Write("locked.rs", "fn hidden_until_readable() {}\n");
        File.SetUnixFileMode(file, UnixFileMode.None);
        var unreadable = !CanRead(file); // root ignores the mode bits
        h.Run();
        if (unreadable)
        {
            Assert.Equal(new SymbolStat("", 0, 0), h.Store.GetSymbolStat(h.Root, "locked.rs"));
            Assert.False(h.HasDef("hidden_until_readable"));
        }

        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Assert.Equal(unreadable ? 1 : 0, h.Run().Indexed);
        Assert.True(h.HasDef("hidden_until_readable"));
        var warm = h.Run();
        Assert.Equal((0, 1), (warm.Read, warm.Skipped));
    }

    [Fact]
    public void A_removed_directory_purges_its_rows()
    {
        using var h = new GraphHarness();
        h.Write("keep.rs", "fn keep() {}\n");
        h.Write("gone/a.rs", "fn gone_a() {}\n");
        h.Write("gone/deep/b.rs", "fn gone_b() {}\n");
        h.Run();
        Directory.Delete(h.Project.Combine("gone"), recursive: true);
        h.Run();

        Assert.Equal(1, h.Store.SymbolFileCount(h.Root));
        Assert.False(h.HasDef("gone_a"));
        Assert.False(h.HasDef("gone_b"));
        Assert.True(h.HasDef("keep"));
    }

    [Fact]
    public void The_count_spans_the_files_the_run_reads()
    {
        using var h = new GraphHarness();
        for (var i = 0; i < 7; i++)
        {
            h.Write($"f{i}.rs", $"fn f{i}() {{}}\n");
        }

        h.Write("notes.md", "not a source file\n");
        var counts = new List<int>();
        Assert.Equal(7, Indexer.Run(h.Cx, h.Project.Path, onCount: counts.Add).Indexed);
        Later(h.Write("f0.rs", "fn changed() {}\n"));
        Assert.Equal(1, Indexer.Run(h.Cx, h.Project.Path, onCount: counts.Add).Indexed);
        Assert.Equal([7, 1], counts);
    }

    [Fact]
    public void A_cold_index_writes_200_files_per_transaction()
    {
        using var h = new GraphHarness();
        for (var i = 0; i < 450; i++)
        {
            h.Write($"f{i:000}.rs", $"fn f{i}() {{}}\n");
        }

        var host = h.Host;
        Indexer.Run(new Ctx(host), h.Project.Path);

        Assert.Equal([200, 200, 50], host.Batches);
        Assert.Equal(450, h.Store.SymbolFileCount(h.Root));
    }

    // Jobs whose parse time falls with their index, so later workers finish first.
    private static List<ParseJob> UnevenJobs(TempDir dir) =>
        [
            .. Enumerable
                .Range(0, 32)
                .Select(i =>
                {
                    var rel = $"f{i:00}.rs";
                    var body = new StringBuilder();
                    for (var j = 0; j < (32 - i) * 40; j++)
                    {
                        body.Append(CultureInfo.InvariantCulture, $"fn f{i}_{j}() {{ g(); }}\n");
                    }

                    return new ParseJob(
                        dir.Write(rel, body.ToString()),
                        rel,
                        (0, 0),
                        null,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                    );
                }),
        ];

    [Fact]
    public void Parsed_files_reach_the_writer_in_walk_order()
    {
        using var dir = new TempDir("rtok-graph-order-");
        var jobs = UnevenJobs(dir);
        var seen = new List<string>();
        ParseWorkers.Each(
            jobs,
            (job, parsed) =>
            {
                Assert.Equal(ParseOutcome.Rows, parsed.Outcome);
                seen.Add(job.Rel);
            }
        );
        Assert.Equal(jobs.Select(j => j.Rel), seen, StringComparer.Ordinal);
    }

    [Fact]
    public void A_failed_write_stops_the_workers()
    {
        // Ten times the window of two results per worker: a run that ignores the failure
        // parses them all.
        var workers = Environment.ProcessorCount;
        var jobs = Enumerable
            .Range(0, workers * 20)
            .Select(i => new ParseJob(
                $"f{i}.rs",
                $"f{i}.rs",
                (0, 0),
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
            ))
            .ToList();
        var writes = 0;
        var parses = 0;
        var e = Assert.Throws<IOException>(() =>
            ParseWorkers.Each(
                jobs,
                _ =>
                {
                    Interlocked.Increment(ref parses);
                    Thread.Sleep(1);
                    return new Parsed(ParseOutcome.Unparsed);
                },
                (_, _) =>
                {
                    writes++;
                    throw new IOException("disk full");
                }
            )
        );
        Assert.Equal("disk full", e.Message);
        Assert.Equal(1, writes);
        Assert.True(parses <= workers * 3, $"{parses} parses after the writer failed");
    }

    [Fact]
    public void A_failed_parse_reaches_the_writer_as_its_exception()
    {
        using var dir = new TempDir("rtok-graph-fail-");
        var jobs = UnevenJobs(dir);
        Assert.Throws<InvalidOperationException>(() =>
            ParseWorkers.Each(jobs, _ => throw new InvalidOperationException("boom"), (_, _) => { })
        );
    }

    [Fact]
    public void Run_changed_indexes_only_the_touched_file()
    {
        const int Files = 20;
        using var h = new GraphHarness();
        for (var i = 0; i < Files; i++)
        {
            h.Write($"f{i}.rs", $"fn f{i}() {{}}\n");
        }

        h.Run();
        var before = h.Store.SymbolCount(h.Root);
        var file = h.Write("f0.rs", "fn f0() {}\nfn touched() {}\n");
        Assert.Equal(1, Indexer.RunChanged(h.Cx, h.Project.Path, [file]).Read);
        Assert.Equal(before + 1, h.Store.SymbolCount(h.Root));
        Assert.All(Enumerable.Range(1, Files - 1), i => Assert.True(h.HasDef($"f{i}")));
        Assert.True(h.HasDef("touched"));
    }

    [Fact]
    public void Run_changed_drops_a_vanished_or_relative_event_path()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "fn alpha() {}\n");
        h.Write("b.rs", "fn beta() {}\n");
        h.Run();
        File.Delete(h.Project.Combine("a.rs"));
        h.Write("b.rs", "fn beta() {}\nfn gamma() {}\n");
        Indexer.RunChanged(h.Cx, h.Project.Path, ["a.rs", "b.rs"]);

        Assert.False(h.HasDef("alpha"));
        Assert.True(h.HasDef("gamma"));
    }

    [Fact]
    public void An_edit_reindexes_only_that_file()
    {
        using var h = new GraphHarness();
        var a = h.Write("a.rs", "fn alpha() {}\n");
        h.Write("b.rs", "fn beta() {}\n");
        h.Run();
        File.WriteAllText(a, "fn alpha() {}\nfn gamma() {}\n");
        Later(a);
        Assert.Equal(1, h.Run().Indexed);
        Assert.True(h.HasDef("gamma"));
        Assert.True(h.HasDef("beta"));
    }

    [Fact]
    public void Ensure_indexes_only_an_empty_root()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "fn alpha() {}\n");
        Assert.Equal(1, Indexer.Ensure(h.Cx, h.Project.Path).Indexed);
        Later(h.Write("b.rs", "fn beta() {}\n"));
        Assert.Equal(0, Indexer.Ensure(h.Cx, h.Project.Path).Indexed);
        Assert.False(h.HasDef("beta"));
    }

    [Fact]
    public void References_carry_their_enclosing_definition()
    {
        using var h = new GraphHarness();
        h.Write(
            "chain.rs",
            "fn a() {\n    b();\n}\nfn b() {\n    c();\n}\nstatic S: u8 = top();\n"
        );
        h.Run();
        List<string> Scope(string name) =>
            [.. h.Store.SymbolRefGroups(h.Root, name).Select(g => $"{g.Path}/{g.Kind}x{g.Count}")];

        Assert.Equal(["chain.rs/bx1"], Scope("c"));
        Assert.Equal(["chain.rs/ax1"], Scope("b"));
        Assert.Equal(["chain.rs/x1"], Scope("top"));
    }

    [Fact]
    public void A_definition_stores_its_byte_span_hash_and_doc()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "/// counts things\nfn keep() {\n    1\n}\n");
        h.Write("gap.rs", "/// lost\n\nfn gone() {}\n");
        h.Run();

        var span = h.Store.GetSymbolSpan(h.Root, "a.rs", "keep", "function", 2);
        Assert.NotNull(span);
        var file = File.ReadAllBytes(h.Project.Combine("a.rs"));
        var bytes = file[(int)span.StartByte..(int)span.EndByte];
        Assert.StartsWith("fn keep", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(Extraction.Sha256(bytes), span.ContentHash);
        Assert.Equal(Extraction.Sha256(file), span.FileSha);
        Assert.Contains(h.Store.SymbolFts(h.Root, "counts things", 5), r => r.Name is "keep");
        Assert.DoesNotContain(h.Store.SymbolFts(h.Root, "lost", 5), r => r.Name is "gone");
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(3, "/// a\n/// b")]
    [InlineData(4, "")]
    public void Doc_above_takes_the_contiguous_comment_lines(int line, string want) =>
        Assert.Equal(want, Extraction.DocAbove(["/// a", "/// b", "fn f() {}", "fn g() {}"], line));

    [Fact]
    public void Doc_above_is_capped_at_512_bytes_on_a_character_boundary()
    {
        var lines = Enumerable
            .Repeat("/// " + new string('é', 100), 4)
            .Append("fn f() {}")
            .ToList();
        var doc = Extraction.DocAbove(lines, 5);
        Assert.True(Encoding.UTF8.GetByteCount(doc) <= Extraction.DocCap);
        Assert.DoesNotContain('�', doc);
        Assert.StartsWith(lines[0] + "\n" + lines[1], doc, StringComparison.Ordinal);
    }

    private static bool CanRead(string file)
    {
        try
        {
            File.ReadAllBytes(file);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
