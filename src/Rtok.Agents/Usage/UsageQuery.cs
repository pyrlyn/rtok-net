using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary><c>[agents.usage]</c>, checked: what the report reads and how it groups it.</summary>
/// <param name="Source"><c>logs</c>, <c>rtok</c> or <c>both</c>.</param>
/// <param name="Daily">Day rows instead of month rows.</param>
/// <param name="ByModel">The middle table lists models instead of agents.</param>
/// <param name="Hosts">The hosts to keep; empty keeps every one.</param>
/// <param name="Zone">The zone days and months are in.</param>
/// <param name="Since">The window's start, unix seconds.</param>
/// <param name="Until">The window's end (exclusive), unix seconds.</param>
internal sealed record UsageQuery(
    string Source,
    bool Daily,
    bool ByModel,
    IReadOnlyList<string> Hosts,
    TimeZoneInfo Zone,
    long Since,
    long Until
)
{
    /// <summary>The query <paramref name="o"/> describes; <paramref name="now"/> anchors a <c>30d</c> window.</summary>
    /// <exception cref="ConfigException">A value is out of its range.</exception>
    public static UsageQuery Of(AgentsUsageSection o, long now)
    {
        var source = o.Source is "logs" or "rtok" or "both"
            ? o.Source
            : throw new ConfigException(
                $"agents.usage.source `{o.Source}`: expected `logs`, `rtok` or `both`"
            );
        var daily = o.Period switch
        {
            "monthly" => false,
            "daily" => true,
            _ => throw new ConfigException(
                $"agents.usage.period `{o.Period}`: expected `monthly` or `daily`"
            ),
        };
        if (o.By is not ("agent" or "model"))
        {
            throw new ConfigException($"agents.usage.by `{o.By}`: expected `agent` or `model`");
        }
        if (
            o.Hosts.FirstOrDefault(h =>
                !ConfigValidator.Hosts.Contains(h, StringComparer.Ordinal)
            ) is
            { } bad
        )
        {
            throw new ConfigException(
                $"unknown host `{bad}` in agents.usage.hosts; known: {string.Join(", ", ConfigValidator.Hosts)}"
            );
        }
        var zone = UsageZone.Find(o.Tz);
        return new UsageQuery(
            source,
            daily,
            o.By is "model",
            [.. o.Hosts],
            zone,
            UsageZone.Since(o.Since, zone, now),
            UsageZone.Until(o.Until, zone)
        );
    }

    /// <summary>True when <paramref name="host"/> is one the query keeps.</summary>
    public bool Wants(string host) =>
        Hosts.Count == 0 || Hosts.Contains(host, StringComparer.Ordinal);
}
