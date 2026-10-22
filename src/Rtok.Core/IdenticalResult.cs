using System.Security.Cryptography;
using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Core;

/// <summary>
/// A tool result whose bytes this session already archived prints as a pointer to that copy
/// (rtok T65.1). Scoped to the context window that wrote it (rtok T127): a sub-agent never gets
/// a pointer to a body the main window or another sub-agent saw.
/// </summary>
public static class IdenticalResult
{
    /// <summary>
    /// The pointer line for <paramref name="body"/> when the session already has it, recording a
    /// <c>dedup</c> Measurement; <see langword="null"/> when it is new, empty or shorter than the
    /// pointer. Looks up before the caller archives.
    /// </summary>
    public static string? Find(
        ICapabilities host,
        string plugin,
        ReadOnlySpan<byte> body,
        string? context
    )
    {
        ArgumentNullException.ThrowIfNull(host);
        if (body.IsEmpty)
        {
            return null;
        }

        if (host.ArchiveInSession(Sha256Hex(body), context) is not { } hit)
        {
            return null;
        }

        var shortId = ArchiveIds.Abbreviate(hit.Id);
        var message =
            $"[rtok {shortId} · identical to a result {Math.Max(hit.Turns, 1)} turns ago · expand: rtok expand {shortId}]";
        var messageBytes = Encoding.UTF8.GetByteCount(message);
        if (body.Length <= messageBytes)
        {
            return null;
        }

        var text = Utf8OrEmpty(body);
        host.Record(
            new Measurement
            {
                Plugin = plugin,
                Kind = "dedup",
                BeforeBytes = body.Length,
                AfterBytes = messageBytes,
                EstBefore = host.Estimate(text, TextClass.Code),
                EstAfter = host.Estimate(message, TextClass.Code),
                RefId = hit.Id,
                CallId = host.CallId,
            }
        );
        return message;
    }

    /// <summary>Lowercase hex SHA-256 of <paramref name="body"/>: the store's archive id.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(body, hash);
        return Convert.ToHexStringLower(hash);
    }

    private static string Utf8OrEmpty(ReadOnlySpan<byte> body)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(body);
        }
        catch (DecoderFallbackException)
        {
            return "";
        }
    }
}
