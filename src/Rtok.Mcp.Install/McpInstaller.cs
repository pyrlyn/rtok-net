using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;

namespace Rtok.Mcp.Install;

/// <summary>The one entry point every host installer calls for rtok's MCP entry (rtok T277).</summary>
public static class McpInstaller
{
    /// <summary>
    /// Installs, updates or removes <paramref name="spec"/>'s entry. Install and update always
    /// write rtok's entry; only remove takes it out (D33), and only an entry that is still rtok's:
    /// one the user edited asks first, a foreign one is left (<see cref="Ownership.Judge"/>).
    /// </summary>
    /// <param name="spec">The surface.</param>
    /// <param name="mode">What to do.</param>
    /// <param name="apply">The run: dry run, <c>--yes</c>, backups.</param>
    /// <param name="disk">Where the file is read and written.</param>
    /// <param name="isBin">Whether a string names the rtok binary (any path, <c>rtok.exe</c>, …).</param>
    /// <returns>
    /// The report line: <c>mcpServers.rtok: rtok mcp --host x</c>, <c>- mcpServers.rtok</c>,
    /// <c>leave …</c>, <c>? …</c>, or <see cref="Apply.NoChanges"/>.
    /// </returns>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file does not parse, or rtok's entry cannot be put in or taken out of it.</exception>
    public static string Apply(
        McpSpec spec,
        McpMode mode,
        Apply apply,
        McpDisk disk,
        Func<string, bool> isBin
    )
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(isBin);
        var text = disk.Read(spec.Path);
        var have = McpConfig.Entry(spec, text);
        var ours = spec.Entry();
        if (mode != McpMode.Remove)
        {
            // Claude.app drops the default "type": "stdio" when it saves its config (rtok T265).
            if (
                have is not null
                && JsonNode.DeepEquals(
                    Ownership.WithoutDefaultType(have),
                    Ownership.WithoutDefaultType(ours)
                )
            )
            {
                return Agent.Sdk.Apply.NoChanges;
            }
            var wrote = $"{spec.At}: {spec.Summary()}";
            disk.Write(apply, spec.Path, McpConfig.With(spec, text, ours), wrote);
            return wrote;
        }
        if (have is null)
        {
            return Agent.Sdk.Apply.NoChanges;
        }
        if (Ownership.Judge(apply, $"{spec.At} in {spec.Path}", have, ours, isBin) is { } leave)
        {
            return leave;
        }
        var removed = $"- {spec.At}";
        disk.Write(apply, spec.Path, McpConfig.Without(spec, text!), removed);
        return removed;
    }
}
