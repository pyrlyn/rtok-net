using Rtok.Plugin.Sdk;

namespace Rtok.Model.Tests;

internal sealed class FakePlugin(string id, bool defaultOn) : IPlugin
{
    public Manifest Manifest { get; } = new(id, Surface.Hook | Surface.Mcp, defaultOn);

    public DashboardPage DashboardPage { get; } =
        new(id, "fake", SavesTokens: true) { Fields = [new("rows", "3")] };
}
