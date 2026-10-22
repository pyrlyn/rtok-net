using Rtok.Core;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>Token sinks: the bytes catalogue plugins were handed, per actionable class (read,
/// cmd, mcp) and sink. The class filter runs before the top-ten cut, so a noisy class that no
/// switch can act on never pushes an actionable one out.</summary>
internal static class SinkRanking
{
    public const int Limit = 10;

    public static List<Sink> Top(IReadOnlyList<MeasurementInput> rows) =>
        [
            .. rows.Where(r => r.BeforeBytes > 0 && Catalogue.IndexOf(r.Plugin) >= 0)
                .Select(r => (Label: Label(r), r.BeforeBytes))
                .Where(x => x.Label.Class is "read" or "cmd" or "mcp")
                .GroupBy(x => x.Label)
                .Select(g => new Sink(
                    g.Key.Class,
                    g.Key.Name,
                    Saturating.Sum(g.Select(x => x.BeforeBytes)),
                    g.Count()
                ))
                .OrderByDescending(s => s.Bytes)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .ThenBy(s => s.Class, StringComparer.Ordinal)
                .Take(Limit),
        ];

    // cmd's ref is `<family>:<archive id>`; a wrapped MCP server's is `<server>:<tool>:<id>`.
    internal static (string Class, string Name) Label(MeasurementInput row) =>
        (row.Plugin, row.Kind, row.RefId) switch
        {
            ("cmd", "wrap", var r) => ("mcp", Before(r, last: true) ?? "mcp"),
            ("cmd", var kind, var r) => ("cmd", Before(r, last: false) ?? kind),
            ("read", _, var r) => ("read", r ?? "unknown"),
            (var plugin, var kind, var r) => (plugin, r ?? kind),
        };

    private static string? Before(string? refId, bool last)
    {
        if (refId is null)
        {
            return null;
        }
        var at = last ? refId.LastIndexOf(':') : refId.IndexOf(':', StringComparison.Ordinal);
        return at > 0 ? refId[..at] : null;
    }
}
