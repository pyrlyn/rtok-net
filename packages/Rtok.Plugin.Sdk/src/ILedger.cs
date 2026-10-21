using System.Text.Json;

namespace Rtok.Plugin.Sdk;

/// <summary>A read-only look back at this session's own traffic, for plugins that need a
/// window rather than a single event.</summary>
public interface ILedger
{
    /// <summary>The raw JSON of the last <paramref name="limit"/> hook inputs in this session,
    /// newest first.</summary>
    IReadOnlyList<string> RecentHookInputs(int limit);

    /// <summary>
    /// The raw JSON of the last <paramref name="limit"/> hook inputs whose
    /// <c>hook_event_name</c> is <paramref name="hookEvent"/>, newest first. The default filters
    /// an unfiltered window in memory; a host with a queryable store should push the filter into
    /// the query instead.
    /// </summary>
    IReadOnlyList<string> RecentHookInputsForEvent(string hookEvent, int limit) =>
        [
            .. RecentHookInputs(limit)
                .Where(body =>
                    string.Equals(HookEventName(body), hookEvent, StringComparison.Ordinal)
                ),
        ];

    /// <summary>How many calls this session has made since the unix timestamp
    /// <paramref name="ts"/>.</summary>
    long CallsSince(long ts);

    /// <summary>The newest <see cref="Measurement.RefId"/> on a measurement row for this
    /// session.</summary>
    string? LastMeasurementRef(string plugin, string kind);

    private static string? HookEventName(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("hook_event_name", out var name)
                && name.ValueKind == JsonValueKind.String
                ? name.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
