using System.Xml.Linq;
using Rtok.Testing;

namespace Rtok.Core.Tests;

// rtok's layers, by project: surfaces → app → core → foundation → Rtok.*.Sdk, with the
// generic Pyrlyn.* packages under all of them. A project may reference its own layer or one
// below, never one above (plan/README.md, Solution layout).
public sealed class ArchitectureTests
{
    private static readonly string[][] Layers =
    [
        ["Pyrlyn.*"],
        ["Rtok.Plugin.Sdk", "Rtok.Agent.Sdk"],
        ["Rtok.Hook.Contracts"],
        ["Rtok.Core", "Rtok.Config", "Rtok.Store", "Rtok.Store.Model", "Rtok.Archive", "Rtok.Log"],
        [
            "Rtok.Plugins.*",
            "Rtok.Agents",
            "Rtok.Mcp.Install",
            "Rtok.Worktree",
            "Rtok.Tasks",
            "Rtok.Measure",
            "Rtok.Report",
            "Rtok.Report.Pdf",
            "Rtok.Doctor",
            "Rtok.Model",
        ],
        [
            "Rtok.Cli",
            "Rtok.Hook",
            "Rtok.Hooks",
            "Rtok.Mcp",
            "Rtok.Proxy",
            "Rtok.Web",
            "Rtok.Tui",
            "Rtok.Demon",
        ],
    ];

    [Fact]
    public void Every_project_has_a_layer()
    {
        var unplaced = Projects().Keys.Where(name => Layer(name) < 0).ToList();
        Assert.Empty(unplaced);
    }

    [Fact]
    public void No_project_references_a_layer_above_its_own() =>
        Assert.Empty(
            Projects()
                .SelectMany(p => p.Value.Where(r => Upward(p.Key, r)).Select(r => $"{p.Key} → {r}"))
        );

    [Fact]
    public void Generic_packages_reference_only_generic_packages() =>
        Assert.Empty(
            Projects()
                .Where(p => p.Key.StartsWith("Pyrlyn.", StringComparison.Ordinal))
                .SelectMany(p =>
                    p.Value.Where(r => !r.StartsWith("Pyrlyn.", StringComparison.Ordinal))
                        .Select(r => $"{p.Key} → {r}")
                )
        );

    [Theory]
    [InlineData("Rtok.Core", "Rtok.Cli", true)]
    [InlineData("Rtok.Plugin.Sdk", "Rtok.Core", true)]
    [InlineData("Rtok.Plugins.Measure", "Rtok.Hooks", true)]
    [InlineData("Rtok.Cli", "Rtok.Plugins.Measure", false)]
    [InlineData("Rtok.Core", "Rtok.Config", false)]
    [InlineData("Rtok.Core", "Rtok.Plugin.Sdk", false)]
    [InlineData("Pyrlyn.FsKit", "Rtok.Agent.Sdk", true)]
    public void An_upward_reference_is_caught(string from, string to, bool upward) =>
        Assert.Equal(upward, Upward(from, to));

    private static bool Upward(string from, string to) => Layer(to) > Layer(from);

    private static int Layer(string project)
    {
        for (var layer = 0; layer < Layers.Length; layer++)
        {
            if (Layers[layer].Any(pattern => Matches(pattern, project)))
            {
                return layer;
            }
        }

        return -1;
    }

    private static bool Matches(string pattern, string project) =>
        pattern.EndsWith('*')
            ? project.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : string.Equals(pattern, project, StringComparison.Ordinal);

    // Every production project (src/, packages/*/src) and the projects it references.
    private static Dictionary<string, string[]> Projects()
    {
        var root = RepoPaths.Root;
        var files = Directory
            .EnumerateFiles(Path.Join(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(
                Directory
                    .EnumerateDirectories(Path.Join(root, "packages"))
                    .Select(dir => Path.Join(dir, "src"))
                    .Where(Directory.Exists)
                    .SelectMany(dir => Directory.EnumerateFiles(dir, "*.csproj"))
            );
        return files.ToDictionary(
            file => Path.GetFileNameWithoutExtension(file),
            file =>
                XDocument
                    .Load(file)
                    .Descendants("ProjectReference")
                    .Select(r =>
                        Path.GetFileNameWithoutExtension((string?)r.Attribute("Include") ?? "")
                    )
                    .ToArray(),
            StringComparer.Ordinal
        );
    }
}
