using System.Text;
using CsCheck;
using Rtok.Testing;

namespace Rtok.Plugins.Inject.Tests;

// Ported from rtok T606: the estimator counts characters, so the read cap does too.
public sealed class ModeReadTests : IDisposable
{
    private static readonly UTF8Encoding Strict = new(false, throwOnInvalidBytes: true);
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Theory]
    [InlineData("x")]
    [InlineData("ж")]
    [InlineData("世")]
    [InlineData("😀")]
    public void AFittingUnicodeModeSurvivesWhole(string unit)
    {
        var text = string.Concat(Enumerable.Repeat(unit, 3000)) + "\nKEEP_THIS_TAIL\n";
        var path = Write("fit.md", Encoding.UTF8.GetBytes(text));
        Assert.Equal(text, ModeFiles.Read(path, null, 800, 4.2f));
    }

    [Fact]
    public void AHugeFileIsCutOnACharacterBoundary()
    {
        var path = Write(
            "huge.md",
            Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 1 << 18)))
        );
        // 800 tokens · 4.2 = 3360 characters, plus the one that lets the fitter see the overshoot.
        var body = ModeFiles.Read(path, null, 800, 4.2f)!;
        Assert.Equal(3361, body.EnumerateRunes().Count());
        Assert.DoesNotContain('�', body);
        Assert.Single(ModeFiles.Read(path, null, 0, 4.2f)!.EnumerateRunes());
    }

    [Fact]
    public void TheReadStopsAtTheByteCap()
    {
        // ceil(1·1.0)+1 = 2 characters, so at most 8 bytes: two four-byte scalars.
        var path = Write("cap.md", Encoding.UTF8.GetBytes("😀😀😀"));
        Assert.Equal("😀😀", ModeFiles.Read(path, null, 1, 1.0f));
    }

    [Fact]
    public void APartialUtf8TailIsDropped()
    {
        Assert.Equal("a", ModeFiles.Decode([0x61, 0xC3], 10));
        Assert.Equal("a", ModeFiles.Decode([0x61, 0xF0, 0x9F, 0x98], 10));
        Assert.Equal("a�b", ModeFiles.Decode([0x61, 0xFF, 0x62], 10));
    }

    [Fact]
    public void AMissingFileGivesTheBuiltIn()
    {
        var missing = Path.Join(_dir.Path, "missing.md");
        Assert.Equal("builtin", ModeFiles.Read(missing, "builtin", 800, 4.2f));
        Assert.Null(ModeFiles.Read(missing, null, 800, 4.2f));
    }

    [Fact]
    public void AnyCappedReadIsValidUtf8AndAPrefixOfTheFile()
    {
        var unit = Gen.OneOfConst<byte[]>(
            [0x61],
            [0x0A],
            [0xC3, 0xA9],
            [0xE4, 0xB8, 0x96],
            [0xF0, 0x9F, 0x98, 0x80],
            [0xC3],
            [0xF0, 0x9F],
            [0xFF],
            [0x80]
        );
        var file = Gen.Select(unit.Array[0, 40], Gen.Byte.Array[0, 4])
            .Select((units, noise) => units.SelectMany(u => u).Concat(noise).ToArray());
        var path = Path.Join(_dir.Path, "any.md");
        Gen.Select(file, Gen.Int[0, 12], Gen.Single[0.1f, 4.2f])
            .Sample(
                (bytes, budget, rate) =>
                {
                    File.WriteAllBytes(path, bytes);
                    var read = ModeFiles.Read(path, null, budget, rate)!;
                    Strict.GetBytes(read);
                    Assert.StartsWith(
                        read,
                        Encoding.UTF8.GetString(bytes),
                        StringComparison.Ordinal
                    );
                },
                iter: 500,
                threads: 1
            );
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Join(_dir.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
