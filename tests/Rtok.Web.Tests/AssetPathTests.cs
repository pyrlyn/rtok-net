using CsCheck;

namespace Rtok.Web.Tests;

public sealed class AssetPathTests
{
    // A file name segment: anything but a separator, NUL or a dot-only name.
    private static readonly Gen<string> Segment = Gen.String[
            Gen.Char.Where(c => c is not ('/' or '\\' or '\0') && !char.IsSurrogate(c)),
            1,
            12
        ]
        .Where(s => s is not ("." or ".."));

    // Raw paths built from the characters that matter to decoding.
    private static readonly Gen<string> Raw = Gen.String[Gen.Char["/.%\\25fF2e0aZ%"], 0, 24];

    [Fact]
    public void Each_segment_is_decoded_exactly_once() =>
        Segment
            .Array[1, 4]
            .Sample(segments =>
            {
                var raw = "/" + string.Join('/', segments.Select(Uri.EscapeDataString));
                Assert.Equal(string.Join('/', segments), AssetPath.Decode(raw));
                // Escaping twice decodes once: the % stays.
                var twice =
                    "/"
                    + string.Join(
                        '/',
                        segments.Select(s => Uri.EscapeDataString(Uri.EscapeDataString(s)))
                    );
                Assert.Equal(
                    string.Join('/', segments.Select(Uri.EscapeDataString)),
                    AssetPath.Decode(twice)
                );
            });

    [Fact]
    public void No_path_resolves_outside_the_client_root()
    {
        var root =
            Path.GetFullPath(Path.Join(Path.GetTempPath(), "rtok-root"))
            + Path.DirectorySeparatorChar;
        Raw.Sample(raw =>
        {
            if (AssetPath.Decode("/" + raw) is not { } path)
            {
                return;
            }
            Assert.DoesNotContain(path.Split('/'), s => s is "" or "." or ".." && path.Length > 0);
            Assert.DoesNotContain('\\', path);
            Assert.DoesNotContain('\0', path);
            Assert.StartsWith(
                root,
                Path.GetFullPath(Path.Join(root, path)) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal
            );
        });
    }

    [Theory]
    [InlineData("/", "")]
    [InlineData("/index.html", "index.html")]
    [InlineData("/a%20b/c.js", "a b/c.js")]
    [InlineData("/route/", "route")]
    [InlineData("/%E2%9C%93", "✓")]
    public void A_valid_path_decodes(string raw, string expected) =>
        Assert.Equal(expected, AssetPath.Decode(raw));

    [Theory]
    [InlineData("/%")]
    [InlineData("/%4")]
    [InlineData("/%G1")]
    [InlineData("/%FF")]
    [InlineData("/%C0%AF")]
    [InlineData("/%ED%A0%80")]
    [InlineData("/a%2Fb")]
    [InlineData("/a%2fb")]
    [InlineData("/a%5Cb")]
    [InlineData("/a%00b")]
    [InlineData("/../etc")]
    [InlineData("/%2E%2E/etc")]
    [InlineData("/a/./b")]
    [InlineData("/a//b")]
    [InlineData("/a\\b")]
    [InlineData("relative")]
    public void Malformed_escapes_bad_bytes_and_separators_are_refused(string raw) =>
        Assert.Null(AssetPath.Decode(raw));
}
