using Rtok.Testing;

namespace Rtok.Log.Tests;

/// <summary>Several processes log through Serilog into one rotating file and lose no line.</summary>
public sealed class WriterProcessTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-writers-");

    [Fact]
    public async Task EveryLineOfEveryWriterIsInTheRotatedFiles()
    {
        // About 7 KB per writer at 1 KB per file: dozens of rotations, all kept.
        const int Writers = 4;
        const int Lines = 120;
        var path = _dir.Combine("rtok.log");

        await Task.WhenAll(
            Enumerable
                .Range(0, Writers)
                .Select(w => WriterProcess.RunAsync(path, 1024, 200, $"w{w}", Lines))
        );

        var files = Directory
            .EnumerateFiles(_dir.Path, "rtok.log*")
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(files.Count > 10, "the writers actually rotated");
        var messages = files
            .SelectMany(n => File.ReadAllLines(_dir.Combine(n!)))
            .Select(l => l[(l.IndexOf(": ", StringComparison.Ordinal) + 2)..])
            .Order(StringComparer.Ordinal);
        var expected = Enumerable
            .Range(0, Writers)
            .SelectMany(w => Enumerable.Range(0, Lines).Select(i => $"w{w}:{i}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected, messages);
    }

    public void Dispose() => _dir.Dispose();
}
