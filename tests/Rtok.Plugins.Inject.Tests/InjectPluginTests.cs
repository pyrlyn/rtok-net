using System.Text.Json.Nodes;
using Rtok.Core;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Testing;

namespace Rtok.Plugins.Inject.Tests;

// Ported from rtok's inject tests (T2.4, T7.1, T188, T531). MemoryHost prices prose at four
// characters a token, so Blob(n) costs exactly n.
public sealed class InjectPluginTests : IDisposable
{
    private static readonly SessionStart Startup = new("startup");
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void InjectionsGoInByPriorityUpToTheBudgetAndTheRestAreNamed()
    {
        File.WriteAllText(Path.Join(_dir.Path, "m.md"), "# m\nrule\n");
        var host = Host(new JsonObject { ["modes_dir"] = _dir.Path, ["modes"] = Names("m") });
        var text = Dispatch(
                host,
                new Offer("c", Blob(500), 1),
                new InjectPlugin(),
                new Offer("a", Blob(500), 9),
                new Offer("b", Blob(300), 7)
            )
            .SessionStart(Startup);

        Assert.Equal($"{Blob(500)}\n{Blob(300)}\ndropped:inject:3\ndropped:c:500", text);
        var row = Assert.Single(host.Recorded());
        Assert.Equal(("inject", "inject"), (row.Plugin, row.Kind));
        Assert.Equal(
            (500 + 300 + 3 + 500, host.Estimate(text, TextClass.Prose)),
            (row.EstBefore, row.EstAfter)
        );
        Assert.Equal("dropped:inject:3", row.RefId);
    }

    [Fact]
    public void AModeGoesInBeforeLowerPrioritiesAndTheCrossingOneKeepsAPrefix()
    {
        File.WriteAllText(Path.Join(_dir.Path, "m.md"), "# m\nrule\n");
        var host = Host(new JsonObject { ["modes_dir"] = _dir.Path, ["modes"] = Names("m") });
        var text = Dispatch(
                host,
                new InjectPlugin(),
                new Offer("a", Blob(500), 9),
                new Offer("c", Blob(1000), 1)
            )
            .SessionStart(Startup);

        Assert.StartsWith($"{Blob(500)}\n# m\nrule\n\nxxxx", text, StringComparison.Ordinal);
        Assert.EndsWith("x\ndropped:c:1000", text, StringComparison.Ordinal);
        Assert.True(host.Estimate(text, TextClass.Prose) <= InjectBudget.DefaultTokens, text);
        Assert.Single(host.Recorded());
    }

    [Fact]
    public void SetupModesInjectAtPriorityFiveOnceASession()
    {
        var host = new MemoryHost("""{"setup": {"modes": ["terse", "yagni"]}}""");
        var plugin = new InjectPlugin();
        var offer = plugin.OnSessionStart(Startup, new Ctx(host));
        Assert.Equal(("inject", 5), (offer?.Plugin, offer?.Priority));

        var dispatcher = Dispatch(host, plugin);
        var text = dispatcher.SessionStart(Startup);
        Assert.Equal(1, Count(text, "# terse"));
        Assert.Equal(1, Count(text, "# yagni"));
        Assert.Equal(ModeFiles.Builtin("terse") + ModeFiles.Builtin("yagni"), text);

        Assert.Equal("", dispatcher.PromptSubmit(new PromptSubmit("hi")));
        Assert.Single(host.Recorded());
    }

    [Fact]
    public void InjectModesWinOverSetupModes()
    {
        var host = new MemoryHost(
            """{"setup": {"modes": ["terse"]}, "plugins": {"inject": {"modes": ["nudges"]}}}"""
        );
        Assert.Equal(ModeFiles.Builtin("nudges"), ModeFiles.Text(new Ctx(host)));
    }

    [Fact]
    public void NoModesOfferNothing() =>
        Assert.Null(new InjectPlugin().OnSessionStart(Startup, new Ctx(new MemoryHost())));

    [Fact]
    public void AliasesNameTheBuiltIns()
    {
        Assert.Equal(ModeFiles.Builtin("terse"), ModeFiles.Builtin("cave"));
        Assert.Equal(ModeFiles.Builtin("yagni"), ModeFiles.Builtin("pony"));
        Assert.Null(ModeFiles.Builtin("tersest"));
    }

    [Fact]
    public void AFileInModesDirWinsOverTheBuiltIn()
    {
        File.WriteAllText(Path.Join(_dir.Path, "terse.md"), "# mine\n");
        var host = Host(
            new JsonObject { ["modes_dir"] = _dir.Path, ["modes"] = Names("terse", "yagni") }
        );
        Assert.Equal("# mine\n" + ModeFiles.Builtin("yagni"), ModeFiles.Text(new Ctx(host)));
    }

    [Theory]
    [InlineData("terse")]
    [InlineData("yagni")]
    [InlineData("nudges")]
    public void EveryShippedModeIsAtMost250Tokens(string name)
    {
        Assert.Contains(name, ModeFiles.Shipped, StringComparer.Ordinal);
        var text = ModeFiles.Builtin(name)!;
        Assert.StartsWith($"# {name}\n", text, StringComparison.Ordinal);
        Assert.InRange(
            TokenEstimator.Estimate(text, TextClass.Prose, EstimatorRates.Default),
            1,
            250
        );
    }

    [Fact]
    public void TwoRunsOverTheSameSessionGiveTheSameBytes()
    {
        File.WriteAllText(Path.Join(_dir.Path, "big.md"), new string('y', 1 << 20));
        File.WriteAllText(
            Path.Join(_dir.Path, "uni.md"),
            string.Concat(Enumerable.Repeat("😀", 1000)) + "\nKEEP_THIS_TAIL\n"
        );
        string Run(string mode)
        {
            var host = Host(new JsonObject { ["modes_dir"] = _dir.Path, ["modes"] = Names(mode) });
            return Dispatch(host, new InjectPlugin()).SessionStart(Startup);
        }

        var big = Run("big");
        Assert.Equal(big, Run("big"));
        Assert.True(TokenEstimator.Estimate(big, TextClass.Prose, EstimatorRates.Default) <= 800);

        var uni = Run("uni");
        Assert.Equal(uni, Run("uni"));
        Assert.Contains("KEEP_THIS_TAIL", uni, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped:inject", uni, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("folder")]
    [InlineData("../escape")]
    [InlineData("")]
    public void AMissingOrUnreadableModeInjectsNothing(string name)
    {
        Directory.CreateDirectory(Path.Join(_dir.Path, "folder.md"));
        File.WriteAllText(Path.Join(Path.GetDirectoryName(_dir.Path)!, "escape.md"), "# out\n");
        try
        {
            var host = Host(new JsonObject { ["modes_dir"] = _dir.Path, ["modes"] = Names(name) });
            Assert.Null(new InjectPlugin().OnSessionStart(Startup, new Ctx(host)));
            Assert.Equal("", Dispatch(host, new InjectPlugin()).SessionStart(Startup));
        }
        finally
        {
            File.Delete(Path.Join(Path.GetDirectoryName(_dir.Path)!, "escape.md"));
        }
    }

    [Fact]
    public void AnUnreadableFileFallsBackToTheBuiltIn()
    {
        Directory.CreateDirectory(Path.Join(_dir.Path, "terse.md"));
        Assert.Equal(
            ModeFiles.Builtin("terse"),
            ModeFiles.Read(Path.Join(_dir.Path, "terse.md"), ModeFiles.Builtin("terse"), 800, 4.2f)
        );
    }

    private static MemoryHost Host(JsonObject inject) =>
        new(new JsonObject { ["plugins"] = new JsonObject { ["inject"] = inject } }.ToJsonString());

    private static JsonArray Names(params string[] names) =>
        [.. names.Select(n => JsonValue.Create(n))];

    private static Dispatcher Dispatch(MemoryHost host, params IPlugin[] plugins) =>
        new(new PluginRegistry(plugins, host.ConfigJson), host);

    private static string Blob(int tokens) => new('x', tokens * 4);

    private static int Count(string text, string part) =>
        text.Split(part, StringSplitOptions.None).Length - 1;

    private sealed class Offer(string id, string text, int priority) : IPlugin
    {
        public Manifest Manifest { get; } = new(id, Surface.Hook, DefaultOn: true);

        public DashboardPage DashboardPage { get; } = new(id, "offer", SavesTokens: false);

        public Injection? OnSessionStart(SessionStart ev, Ctx cx) => new(id, text, priority);
    }
}
