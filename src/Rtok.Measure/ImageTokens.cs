using System.Buffers.Binary;

namespace Rtok.Measure;

/// <summary>
/// What an <c>image</c> block costs. The pixel size comes from the PNG <c>IHDR</c> or the JPEG
/// <c>SOF</c> header (a fixed-offset parse, no image library); tokens follow the provider's
/// published formula.
/// </summary>
internal static class ImageTokens
{
    /// <summary>
    /// Base64 characters decoded to find the header: a JPEG <c>SOF</c> sits after EXIF and ICC
    /// segments, a few KB into a screenshot; the cap bounds the work per block.
    /// </summary>
    private const int HeaderB64 = 64 * 1024;

    // Anthropic vision docs, "Resolution and token cost" (read 2026-09-24): an image costs
    // ⌈width / 28⌉ × ⌈height / 28⌉ visual tokens after downscaling to the model's limits.
    // High-resolution tier (Claude 4.7 and later): long edge 2576 px, 4784 tokens.
    private const long Patch = 28;
    private const long LongEdge = 2576;
    private const long MaxTokens = 4784;

    /// <summary>
    /// Estimated visual tokens for a <paramref name="w"/>×<paramref name="h"/> image. One over the
    /// cap is billed at the cap.
    /// </summary>
    public static long Tokens(long w, long h)
    {
        var longEdge = Math.Max(w, h);
        if (longEdge > LongEdge)
        {
            w = w * LongEdge / longEdge;
            h = h * LongEdge / longEdge;
        }
        return Math.Min(CeilDiv(w, Patch) * CeilDiv(h, Patch), MaxTokens);
    }

    /// <summary>The decoded byte length of base64 <paramref name="data"/>; padding and whitespace are skipped.</summary>
    public static long DecodedLength(string data)
    {
        long n = 0;
        foreach (var c in data)
        {
            if (Sextet(c) >= 0)
            {
                n++;
            }
        }
        return n * 3 / 4;
    }

    /// <summary>The size of a base64 PNG or JPEG; null for another format or a truncated header.</summary>
    public static (long W, long H)? Dims(string data)
    {
        var b = Decode(data.AsSpan(0, Math.Min(data.Length, HeaderB64)));
        if (
            b.AsSpan().StartsWith(PngSignature)
            && b.Length >= 16
            && b.AsSpan(12, 4).SequenceEqual("IHDR"u8)
        )
        {
            return Be32(b, 16) is { } w && Be32(b, 20) is { } h ? (w, h) : null;
        }
        if (b.Length < 2 || b[0] != 0xFF || b[1] != 0xD8)
        {
            return null;
        }
        var i = 2;
        while (i + 3 < b.Length)
        {
            if (b[i] != 0xFF)
            {
                return null;
            }
            var m = b[i + 1];
            switch (m)
            {
                case 0xFF:
                    i += 1;
                    break;
                case 0x01 or (>= 0xD0 and <= 0xD8):
                    i += 2;
                    break;
                // SOF0–SOF15 except DHT (C4), JPG (C8) and DAC (CC): precision, height, width.
                case >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC):
                    return Be16(b, i + 5) is { } h && Be16(b, i + 7) is { } w ? (w, h) : null;
                default:
                    if (Be16(b, i + 2) is not { } len)
                    {
                        return null;
                    }
                    i += 2 + (int)len;
                    break;
            }
        }
        return null;
    }

    /// <summary>The PNG file signature; byte 0x89 is not ASCII, so no <c>u8</c> literal spells it.</summary>
    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static long CeilDiv(long a, long b) => (a + b - 1) / b;

    private static long? Be16(byte[] b, int i) =>
        i >= 0 && i + 2 <= b.Length ? BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i)) : null;

    private static long? Be32(byte[] b, int i) =>
        i >= 0 && i + 4 <= b.Length ? BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(i)) : null;

    private static int Sextet(char c) =>
        c switch
        {
            >= 'A' and <= 'Z' => c - 'A',
            >= 'a' and <= 'z' => c - 'a' + 26,
            >= '0' and <= '9' => c - '0' + 52,
            '+' or '-' => 62,
            '/' or '_' => 63,
            _ => -1,
        };

    /// <summary>Standard or URL-safe base64; anything outside the alphabet is skipped.</summary>
    private static byte[] Decode(ReadOnlySpan<char> s)
    {
        var out_ = new List<byte>(s.Length / 4 * 3);
        var (acc, bits) = (0, 0);
        foreach (var c in s)
        {
            var v = Sextet(c);
            if (v < 0)
            {
                continue;
            }
            acc = ((acc << 6) | v) & 0xFFFF;
            bits += 6;
            if (bits >= 8)
            {
                bits -= 8;
                out_.Add((byte)(acc >> bits));
            }
        }
        return [.. out_];
    }
}
