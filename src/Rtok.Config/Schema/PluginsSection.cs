namespace Rtok.Config;

/// <summary>one field per catalogue id, in `CATALOGUE` order</summary>
/// <remarks>Read from [plugins].</remarks>
public sealed class PluginsSection
{
    /// <summary>The [plugins.measure] table.</summary>
    public MeasureSection Measure { get; set; } = new();

    /// <summary>The [plugins.cmd] table.</summary>
    public CmdSection Cmd { get; set; } = new();

    /// <summary>The [plugins.read] table.</summary>
    public ReadSection Read { get; set; } = new();

    /// <summary>The [plugins.json_tree] table.</summary>
    public JsonTreeSection JsonTree { get; set; } = new();

    /// <summary>The [plugins.archive] table.</summary>
    public ArchiveSection Archive { get; set; } = new();

    /// <summary>The [plugins.proxy] table.</summary>
    public ProxyPluginSection Proxy { get; set; } = new();

    /// <summary>The [plugins.inject] table.</summary>
    public InjectSection Inject { get; set; } = new();

    /// <summary>The [plugins.guard] table.</summary>
    public GuardSection Guard { get; set; } = new();

    /// <summary>The [plugins.memory] table.</summary>
    public MemorySection Memory { get; set; } = new();

    /// <summary>The [plugins.graph] table.</summary>
    public GraphSection Graph { get; set; } = new();

    /// <summary>The [plugins.toon] table.</summary>
    public ToonSection Toon { get; set; } = new();

    /// <summary>The [plugins.compress] table.</summary>
    public CompressSection Compress { get; set; } = new();

    /// <summary>The [plugins.docs] table.</summary>
    public DocsSection Docs { get; set; } = new();

    /// <summary>The [plugins.wasm] table.</summary>
    public WasmSection Wasm { get; set; } = new();
}
