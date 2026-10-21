using Rtok.Testing;

namespace Rtok.Testing.Tests;

public sealed class TempDirTests
{
    [Fact]
    public void Dispose_removes_the_directory_and_everything_in_it()
    {
        string root;
        using (var dir = new TempDir())
        {
            root = dir.Path;
            var file = dir.Write("a/b/c.txt", "x");
            File.SetAttributes(file, FileAttributes.ReadOnly);
            Assert.True(File.Exists(file));
        }

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Each_instance_gets_its_own_directory()
    {
        using var first = new TempDir();
        using var second = new TempDir();

        Assert.NotEqual(first.Path, second.Path, StringComparer.Ordinal);
    }
}
