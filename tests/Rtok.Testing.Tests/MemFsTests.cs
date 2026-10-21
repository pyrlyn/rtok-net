using CsCheck;
using Rtok.Testing;

namespace Rtok.Testing.Tests;

public sealed class MemFsTests
{
    [Fact]
    public void Windows_names_ignore_case_and_linux_names_do_not()
    {
        var windows = MemFs.Windows();
        windows.Directory.CreateDirectory(@"C:\repo");
        windows.File.WriteAllText(@"C:\repo\README.md", "x");
        var linux = MemFs.Linux();
        linux.Directory.CreateDirectory("/repo");
        linux.File.WriteAllText("/repo/README.md", "x");

        Assert.True(windows.File.Exists(@"C:\REPO\readme.md"));
        Assert.False(linux.File.Exists("/REPO/readme.md"));
    }

    [Fact]
    public void Nothing_reaches_the_host_disk()
    {
        var fs = MemFs.Linux();
        var path = "/rtok-memfs-probe/" + Guid.NewGuid().ToString("N");

        fs.Directory.CreateDirectory(path);

        Assert.True(fs.Directory.Exists(path));
        Assert.False(Directory.Exists(path));
    }

    // CsCheck sample: any bytes written come back unchanged.
    [Fact]
    public void Bytes_round_trip() =>
        Gen
            .Byte.Array[0, 4096]
            .Sample(bytes =>
            {
                var fs = MemFs.Create();
                fs.File.WriteAllBytes("blob.bin", bytes);
                return fs.File.ReadAllBytes("blob.bin").AsSpan().SequenceEqual(bytes);
            });
}
