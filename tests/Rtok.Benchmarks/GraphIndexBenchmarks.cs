using BenchmarkDotNet.Attributes;
using Rtok.Plugin.Sdk;
using Rtok.Plugins.Graph;
using Rtok.Plugins.Graph.Tests;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Benchmarks;

// rtok tests/graph_bench.rs: a warm re-index of an unchanged 3000-file repository, the walk and
// stat gate every tool call pays (T24's bar: under 0.1 s), and one edited file through the
// watcher's path, which reads only that file (rtok T35.4's bar: under 10 ms, the write included).
[MemoryDiagnoser]
public class GraphIndexBenchmarks : IDisposable
{
    private TempDir? _data;
    private TempDir? _repo;
    private RtokStore? _store;
    private Ctx? _cx;
    private int _edits;

    [GlobalSetup]
    public void Setup()
    {
        _data = new TempDir("rtok-bench-graph-data-");
        _repo = new TempDir("rtok-bench-graph-");
        for (var i = 0; i < 3000; i++)
        {
            _repo.Write(
                $"f{i}.rs",
                $"fn f{i}() {{\n    f{(i + 1) % 3000}();\n    f{(i + 2) % 3000}();\n}}\n"
            );
        }

        _store = RtokStore.Open(
            new StoreOptions(_data.Combine("rtok.db")) { ArchiveDir = _data.Combine("archive") }
        );
        _cx = new Ctx(new StoreHost(_store) { Cwd = _repo.Path });
        Indexer.Run(_cx, _repo.Path);
    }

    [Benchmark]
    public int WarmReindex() => Indexer.Run(_cx!, _repo!.Path).Read;

    [Benchmark]
    public int EditOneFile()
    {
        // Lengths alternate, so the stat gate always sees a change.
        var path = _repo!.Write(
            "f0.rs",
            "fn f0() {}\n" + new string('/', 2 + (_edits++ % 2)) + "\n"
        );
        return Indexer.RunChanged(_cx!, _repo.Path, [path]).Read;
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _store?.Dispose();
        _repo?.Dispose();
        _data?.Dispose();
        GC.SuppressFinalize(this);
    }
}
