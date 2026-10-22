using System.Text;

namespace Rtok.Web;

/// <summary>The client file a URL path names: each segment percent-decoded exactly once.</summary>
public static class AssetPath
{
    private static readonly UTF8Encoding Strict = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    /// <summary>
    /// Decodes <paramref name="rawPath"/> (the request path as sent, without the query). Null for
    /// a path that names no file: a malformed escape, bytes that are not UTF-8, a segment that
    /// decodes to a separator or NUL (<c>%2F</c> must not nest a path), an empty segment, or
    /// <c>.</c> and <c>..</c>. One trailing <c>/</c> is dropped. The result is relative, <c>/</c>-separated, and stays inside the
    /// client root; the empty path is the root.
    /// </summary>
    /// <param name="rawPath">The raw path, starting with <c>/</c>.</param>
    /// <returns>The relative file path, or null.</returns>
    public static string? Decode(string rawPath)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        if (!rawPath.StartsWith('/'))
        {
            return null;
        }
        var trimmed = rawPath[1..];
        if (trimmed.EndsWith('/'))
        {
            trimmed = trimmed[..^1];
        }
        if (trimmed.Length == 0)
        {
            return "";
        }
        var segments = trimmed.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            if (DecodeSegment(segments[i]) is not { } decoded)
            {
                return null;
            }
            segments[i] = decoded;
        }
        return string.Join('/', segments);
    }

    private static string? DecodeSegment(string segment)
    {
        var bytes = new List<byte>(segment.Length);
        for (var i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            if (c == '%')
            {
                if (
                    i + 2 >= segment.Length
                    || !char.IsAsciiHexDigit(segment[i + 1])
                    || !char.IsAsciiHexDigit(segment[i + 2])
                )
                {
                    return null;
                }
                bytes.Add(Convert.ToByte(segment.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (
                char.IsHighSurrogate(c)
                && i + 1 < segment.Length
                && char.IsLowSurrogate(segment[i + 1])
            )
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(segment.Substring(i, 2)));
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                return null;
            }
            else if (c > 0x7F)
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
            }
            else
            {
                bytes.Add((byte)c);
            }
        }
        string decoded;
        try
        {
            decoded = Strict.GetString([.. bytes]);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        return
            decoded.Length == 0
            || decoded is "." or ".."
            || decoded.AsSpan().IndexOfAny('/', '\\', '\0') >= 0
            ? null
            : decoded;
    }
}
