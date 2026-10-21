using System.Text.Json.Serialization;

namespace Rtok.Mcp.Install;

/// <summary>
/// One surface's MCP status, as <c>agents info</c>, <c>agents list</c>, their <c>--json</c> and
/// the web model all report it (rtok T278).
/// </summary>
/// <param name="Surface">The surface label: <c>cli</c>, <c>desktop</c>, <c>stable</c>, …</param>
/// <param name="File">The config file, the home directory spelled <c>~</c>.</param>
/// <param name="Entry">What the file holds under rtok's name.</param>
/// <param name="Diff">For a stale entry, what it runs instead; for an unreadable file, why.</param>
/// <param name="Plugin">What else serves rtok's server there: the rtok plugin, Grok's Claude import.</param>
public sealed record McpRow(
    string Surface,
    string File,
    McpEntryState Entry,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Diff,
    string? Plugin
)
{
    /// <summary>rtok's server reaches this surface as rtok runs it: its own entry, or a plugin.</summary>
    [JsonIgnore]
    public bool Serves => Entry == McpEntryState.Present || Plugin is not null;

    /// <summary>Something under rtok's name is there (a stale entry too, which update rewrites), or a plugin serves it.</summary>
    [JsonIgnore]
    public bool Found => Entry != McpEntryState.Missing || Plugin is not null;

    /// <summary>
    /// <c>entry &lt;file&gt;</c>, <c>stale (&lt;diff&gt;)</c> or <c>missing (&lt;file&gt; has no "rtok")</c>,
    /// then <c>plugin &lt;name&gt; (serves "rtok")</c> when a plugin serves the surface too.
    /// </summary>
    public string Text()
    {
        var name = McpServer.Rtok.Name;
        var entry = (Entry, Diff) switch
        {
            (McpEntryState.Present, _) => $"entry {File}",
            (McpEntryState.Stale, var d) => $"stale ({d})",
            (McpEntryState.Missing, _) when Plugin is not null => null,
            (McpEntryState.Missing, { } error) => $"missing ({error})",
            _ => $"missing ({FileName(File)} has no \"{name}\")",
        };
        var plugin = Plugin is null ? null : $"plugin {Plugin} (serves \"{name}\")";
        return string.Join(", ", new[] { entry, plugin }.OfType<string>());
    }

    private static string FileName(string file) => file[(file.LastIndexOfAny(['/', '\\']) + 1)..];
}
