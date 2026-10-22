using Rtok.Plugin.Sdk;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Plugins.Graph.Tests;

/// <summary>A store and a project directory, both temporary, and a <see cref="Ctx"/> on them
/// whose <c>[plugins.graph]</c> section is <see cref="GraphJson"/>.</summary>
internal sealed class GraphHarness : IDisposable
{
    private readonly TempDir _data = new("rtok-graph-data-");
    private RtokStore? _store;
    private StoreHost? _host;
    private string? _graphJson;

    public GraphHarness(string? graphJson = null)
    {
        Project = new TempDir("rtok-graph-");
        GraphJson = graphJson;
    }

    public TempDir Project { get; }

    /// <summary>The project directory, canonical: the index key.</summary>
    public string Root => GraphRoot.Check(Project.Path);

    /// <summary>The <c>[plugins.graph]</c> JSON object <see cref="Host"/> reads; setting it
    /// starts a new host, so archives and measurements start empty.</summary>
    public string? GraphJson
    {
        get => _graphJson;
        set
        {
            _graphJson = value;
            _host = null;
        }
    }

    public RtokStore Store =>
        _store ??= RtokStore.Open(
            new StoreOptions(_data.Combine("rtok.db")) { ArchiveDir = _data.Combine("archive") }
        );

    /// <summary>One host per configuration, its working directory the project.</summary>
    public StoreHost Host =>
        _host ??= new(
            Store,
            GraphJson is null ? null : "{\"plugins\":{\"graph\":" + GraphJson + "}}"
        )
        {
            Cwd = Project.Path,
        };

    public Ctx Cx => new(Host);

    public string Write(string relative, string content) => Project.Write(relative, content);

    public IndexReport Run(bool dryRun = false) => Indexer.Run(Cx, Project.Path, dryRun);

    public bool HasDef(string name) => Store.SymbolDefs(Root, name).Count > 0;

    public void Dispose()
    {
        _store?.Dispose();
        Project.Dispose();
        _data.Dispose();
    }
}
