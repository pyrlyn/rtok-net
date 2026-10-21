using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Mcp.Install;
using Rtok.Plugin.Sdk;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Model.Tests;

/// <summary>A user home with a config, a git project and a store with one session, call, usage
/// row and measurement.</summary>
internal sealed class ModelFixture : IDisposable
{
    public const long Now = 1_800_000_000;

    private readonly TempDir _dir = new("rtok-model-");

    public ModelFixture()
    {
        Directory.CreateDirectory(Path.Join(Project, ".git"));
        Directory.CreateDirectory(Home);
        File.WriteAllText(
            UserFile,
            "[otel]\nheaders = \"k=secret\"\n[proxy]\nport = 1001\n[plugins.docs]\nenabled = true\n"
        );
        Store = RtokStore.Open(
            new StoreOptions(_dir.Combine("data", "rtok.db"))
            {
                ArchiveDir = _dir.Combine("data", "archive"),
                Time = Clock,
            }
        );
        var host = Store.HostId("claude");
        Store.UpsertSession("s1", host, "proj", "/work/proj", "claude");
        var (provider, model) = Store.UpsertModel("anthropic", "claude-x");
        var call = Store.InsertCall("s1", "proxy", "api", host, provider, model, name: "messages");
        Store.InsertUsage("s1", "claude-x", "messages", 100, 10, 20, 5, call);
        Store.InsertMeasurement(
            "s1",
            new MeasurementInput("measure", "read", 1000, 400, 250, 100, CallId: call)
        );
    }

    public ManualClock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(Now));

    public RtokStore Store { get; }

    public string Home => Path.Join(_dir.Path, "user", ".rtok");

    public string Project => Path.Join(_dir.Path, "project");

    public string UserFile => Path.Join(Home, "config.toml");

    public ConfigLoadOptions Options =>
        new()
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = Path.Join(_dir.Path, "user"),
                ["RTOK_HOME"] = Home,
            },
            WorkingDirectory = Project,
        };

    public static IEnumerable<IPlugin> Linked() =>
        [new FakePlugin("measure", defaultOn: true), new FakePlugin("docs", defaultOn: false)];

    public ModelSources Sources() => ModelSources.Create(Store, Linked, _ => Hosts());

    /// <summary>A sandboxed Linux home on an in-memory disk with no apps: nothing on the machine
    /// is probed or run.</summary>
    public static HostContext Hosts() =>
        new(
            new HostEnvironment(
                "/home/u",
                McpOs.Linux,
                v =>
                    v switch
                    {
                        "PATH" => "/home/u/bin",
                        HostEnvironment.SandboxVariable => "1",
                        _ => null,
                    }
            ),
            new McpDisk(MemFs.Linux()),
            (_, _, _) => ("1.0.0 (fake)", ""),
            new RtokBinary("/opt/rtok/bin/rtok"),
            new HostSettings(Backup: false)
        );

    public ModelContext Context() => new(ConfigLoader.Load(Options), Clock);

    public OperatorModel Model(ModelSources? sources = null, Action<Snapshot>? built = null) =>
        new(sources ?? Sources(), ["measure", "docs"], Options, Clock, built);

    public void Dispose()
    {
        Store.Dispose();
        _dir.Dispose();
    }
}
