using System.Runtime.CompilerServices;

namespace Pyrlyn.CommandLine.Docs.Tests;

/// <summary>
/// The expected files under <c>tests/goldens</c>. With <c>PYRLYN_UPDATE_GOLDENS=1</c> a check
/// rewrites the golden instead of comparing, for a deliberate change to the output.
/// </summary>
internal static class Goldens
{
    private static readonly bool Update = string.Equals(
        Environment.GetEnvironmentVariable("PYRLYN_UPDATE_GOLDENS"),
        "1",
        StringComparison.Ordinal
    );

    internal static string Root { get; } = Source();

    /// <summary>The golden's text.</summary>
    internal static string Read(string name) => File.ReadAllText(Path.Combine(Root, name));

    /// <summary>Asserts the files equal the goldens in <paramref name="folder"/> byte for byte, and no golden is left over.</summary>
    internal static void Match(string folder, IReadOnlyList<GeneratedFile> files)
    {
        var dir = Path.Combine(Root, folder);
        if (Update)
        {
            Directory.CreateDirectory(dir);
            foreach (var stale in Directory.GetFiles(dir))
            {
                File.Delete(stale);
            }
            foreach (var file in files)
            {
                File.WriteAllText(Path.Combine(dir, file.Name), file.Content);
            }
            return;
        }
        Assert.Equal(
            Directory.GetFiles(dir).Select(Path.GetFileName).Order(StringComparer.Ordinal),
            files.Select(f => f.Name).Order(StringComparer.Ordinal)
        );
        foreach (var file in files)
        {
            Assert.Equal(File.ReadAllText(Path.Combine(dir, file.Name)), file.Content);
        }
    }

    /// <summary>Asserts the text equals the golden byte for byte.</summary>
    internal static void Match(string name, string text)
    {
        var path = Path.Combine(Root, name);
        if (Update)
        {
            File.WriteAllText(path, text);
            return;
        }
        Assert.Equal(File.ReadAllText(path), text);
    }

    private static string Source([CallerFilePath] string file = "") =>
        Path.Combine(Path.GetDirectoryName(file)!, "goldens");
}
