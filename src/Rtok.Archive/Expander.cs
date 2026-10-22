using System.Diagnostics.CodeAnalysis;
using System.Text;
using Rtok.Config;
using Rtok.Core;
using Rtok.Plugin.Sdk;
using Rtok.Store;

namespace Rtok.Archive;

/// <summary>
/// The one expand handler: <c>rtok expand</c> and the MCP <c>expand</c> tool both call
/// <see cref="Run"/>. A range is checked before anything changes: first its syntax, then,
/// once the body is read, against its real line count. Only then does expanding freeze every
/// pointer to the archive (the proxy sends the original from the next request on) and record
/// the cost as one <c>expand</c> Measurement, both in one transaction.
/// </summary>
/// <param name="store">The store holding the archive.</param>
/// <param name="config">For <c>[expand] max_lines</c> and the estimator rates.</param>
/// <param name="session">The expander's session: <see cref="CliSession"/> for the CLI.</param>
/// <param name="callId">The call the Measurement belongs to, if any.</param>
public sealed class Expander(
    RtokStore store,
    RtokConfig config,
    string session,
    long? callId = null
)
{
    /// <summary>The session <c>rtok expand</c> runs under.</summary>
    public const string CliSession = "expand";

    private const string JsonTreePrefix = "[json-tree ";
    private const string ToonPrefix = "[toon ";

    public ExpandResult Run(ExpandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (
            request.Lines is { } spec
            && !LineRange.TryParse(spec, long.MaxValue, out _, out var malformed)
        )
        {
            return ExpandResult.Fail(malformed);
        }
        if (!TryResolve(request.Id, out var id, out var ambiguous))
        {
            return ExpandResult.Fail(ambiguous);
        }
        if (store.GetArchive(id) is not { } bytes)
        {
            return ExpandResult.Fail(UnknownIdMessage(request.Id));
        }
        var text = Encoding.UTF8.GetString(bytes);
        var lines = ArchiveLines.Split(text);
        LineRange? range = null;
        if (request.Lines is { } wanted)
        {
            if (!LineRange.TryParse(wanted, lines.Count, out var parsed, out var outside))
            {
                return ExpandResult.Fail(outside);
            }
            range = parsed;
        }
        Freeze(id, bytes.Length, text);
        var maxLines = config.Expand.MaxLines;
        if (request.Lines is null && request.Grep is null && maxLines == 0)
        {
            return new(bytes, null);
        }
        var rendered = ArchiveLines.Render(
            lines,
            id,
            range,
            request.Grep,
            request.Context,
            maxLines
        );
        return new(rendered.Length == 0 ? [] : Encoding.UTF8.GetBytes(rendered + "\n"), null);
    }

    /// <summary>The error for an id with no archive. One that cannot be an id at all gets a
    /// hint: agents guess a stdin form (<c>cmd | rtok expand -</c>), and nothing reads
    /// stdin.</summary>
    public static string UnknownIdMessage(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length is >= ArchiveIds.ShortLength and <= 64 && IsHex(id)
            ? $"unknown archive id: {id}"
            : $"unknown archive id: {id} (expand takes the hex id from an \"expand <id>\" trailer; it does not read stdin)";
    }

    /// <summary>A hex prefix of 8 to 63 characters names the one archive starting with it;
    /// with no match it passes through to be reported unknown. Two matches are an error that
    /// names each candidate four characters longer, enough to pick one.</summary>
    private bool TryResolve(string id, out string resolved, [NotNullWhen(false)] out string? error)
    {
        error = null;
        resolved = IsHex(id) ? id.ToLowerInvariant() : id;
        if (resolved.Length is < ArchiveIds.ShortLength or >= 64 || !IsHex(resolved))
        {
            return true;
        }
        var hits = store.ArchiveIdsWithPrefix(resolved, 2);
        if (hits.Count == 1)
        {
            resolved = hits[0];
        }
        else if (hits.Count > 1)
        {
            var longer = hits.Select(h => h[..Math.Min(h.Length, id.Length + 4)]);
            error = $"archive id prefix {id} is ambiguous; use one of: {string.Join(", ", longer)}";
            return false;
        }
        return true;
    }

    /// <summary>Freezes the pointers to <paramref name="id"/> and charges the expansion to the
    /// plugin whose pointer it was.</summary>
    private void Freeze(string id, long bytes, string text)
    {
        var plugin = store.LivePointer(id) switch
        {
            { } p when p.StartsWith(ToonPrefix, StringComparison.Ordinal) => "toon",
            { } p when p.StartsWith(JsonTreePrefix, StringComparison.Ordinal) => "json_tree",
            _ => "archive",
        };
        var e = config.Estimator;
        var rates = new EstimatorRates((float)e.Code, (float)e.Prose, (float)e.Json, (float)e.Cjk);
        store.FreezeRecorded(
            id,
            session,
            new MeasurementInput(
                plugin,
                "expand",
                BeforeBytes: 0,
                AfterBytes: bytes,
                EstBefore: 0,
                EstAfter: TokenEstimator.Estimate(text, TextClass.Code, rates),
                RefId: id,
                CallId: callId
            )
        );
    }

    private static bool IsHex(string text) => text.Length > 0 && text.All(char.IsAsciiHexDigit);
}
