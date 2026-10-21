using Rtok.Testing;

namespace Rtok.Config.Tests;

public sealed class ReferenceConfigTests
{
    private static readonly string File = Path.Join(
        RepoPaths.Root,
        "src",
        "Rtok.Config",
        "config",
        "default.toml"
    );

    // A mismatch writes default.received.toml beside the file; review it and move it over.
    [Fact]
    public void Default_toml_matches_the_schema_and_its_doc_comments()
    {
        var expected = ReferenceConfig.Render();
        var received = Path.ChangeExtension(File, ".received.toml");
        if (!string.Equals(System.IO.File.ReadAllText(File), expected, StringComparison.Ordinal))
        {
            System.IO.File.WriteAllText(received, expected);
            Assert.Fail($"{File} is out of date; review {received} and move it over");
        }
        System.IO.File.Delete(received);
    }

    [Fact]
    public void The_embedded_reference_is_the_checked_in_file() =>
        Assert.Equal(System.IO.File.ReadAllText(File), ConfigFile.Reference);

    [Fact]
    public void The_reference_names_no_plan_ids() =>
        Assert.DoesNotMatch(@"\bT\d+(\.\d+)?\b|\bN\d+\b|\bD\d+\b", ConfigFile.Reference);

    [Fact]
    public void The_reference_loads_as_the_defaults()
    {
        using var home = new TempDir();
        var file = home.Write("config.toml", ConfigFile.Reference);
        var loaded = ConfigLoader.LoadFile(
            file,
            new ConfigLoadOptions
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["RTOK_HOME"] = home.Path,
                },
            }
        );
        Assert.Empty(loaded.Issues);
        Assert.All(
            loaded.Entries,
            e => Assert.Equal(Pyrlyn.Config.Layered.ConfigSourceKind.Defaults, e.Source.Kind)
        );
    }
}
