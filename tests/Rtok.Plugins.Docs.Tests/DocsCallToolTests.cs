using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugins.Docs.Tests;

public sealed class DocsCallToolTests : IDisposable
{
    private readonly DocsHarness _h = new();
    private readonly DocsPlugin _plugin;

    public DocsCallToolTests()
    {
        _h.Write("Cargo.lock", DocsHarness.CargoLockText);
        _plugin = new DocsPlugin(_ => _h.Service);
    }

    public void Dispose() => _h.Dispose();

    private string? Call(string name, string args)
    {
        using var doc = JsonDocument.Parse(args);
        return _plugin.CallTool(name, doc.RootElement, _h.Cx);
    }

    [Fact]
    public void Another_plugins_tool_is_not_answered() => Assert.Null(Call("symbol", "{}"));

    [Fact]
    public void Resolve_reads_the_lockfile_in_the_dispatch_directory() =>
        Assert.Equal(
            """{"name":"serde","version":"1.0.210","cached":false}""",
            Call("docs_resolve", """{"name":"serde"}""")
        );

    [Fact]
    public void Query_and_get_reach_the_service()
    {
        Assert.Equal(
            "not cached: rtok docs fetch serde",
            Call("docs_query", """{"name":"serde","query":"data","version":""}""")
        );
        Assert.Equal(
            "unknown doc id: 99",
            Assert.Throws<DocsException>(() => Call("docs_get", """{"id":99}""")).Message
        );
    }

    [Theory]
    [InlineData("docs_resolve", "{}", "missing `name`")]
    [InlineData("docs_query", """{"name":"serde"}""", "missing `query`")]
    [InlineData("docs_get", """{"id":"7"}""", "missing `id`")]
    [InlineData("docs_get", "[]", "missing `id`")]
    public void A_missing_argument_is_a_tool_error(string name, string args, string message) =>
        Assert.StartsWith(
            message,
            Assert.Throws<ArgumentException>(() => Call(name, args)).Message,
            StringComparison.Ordinal
        );

    [Fact]
    public void Without_a_service_a_docs_tool_fails()
    {
        using var doc = JsonDocument.Parse("""{"name":"serde"}""");

        Assert.Throws<InvalidOperationException>(() =>
            new DocsPlugin().CallTool("docs_resolve", doc.RootElement, new Ctx(new MemoryHost()))
        );
    }
}
