using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

public sealed class ImageTokensTests
{
    [Theory]
    [InlineData(28, 28, 1)]
    [InlineData(29, 57, 6)]
    [InlineData(1000, 800, 36 * 29)]
    [InlineData(2576, 1288, 92 * 46)]
    [InlineData(2576, 2576, 4784)] // the high-res tier: 92 × 92 patches, billed at the cap
    [InlineData(5152, 1000, 92 * 18)] // scaled to a 2576 px long edge first
    public void Tokens_are_28_px_patches_up_to_the_cap(long w, long h, long tokens) =>
        Assert.Equal(tokens, ImageTokens.Tokens(w, h));

    [Fact]
    public void Sizes_come_from_png_ihdr_and_jpeg_sof()
    {
        Assert.Equal((1400L, 900L), ImageTokens.Dims(Png(1400, 900)));
        Assert.Equal((640L, 480L), ImageTokens.Dims(Jpeg(640, 480)));
        Assert.Null(ImageTokens.Dims(Convert.ToBase64String("GIF89a"u8)));
    }
}
