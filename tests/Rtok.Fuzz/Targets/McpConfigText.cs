using System.Text.Json.Nodes;
using Rtok.Mcp.Install;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any host config text in each format through <see cref="McpConfig"/>: it fails only with
/// <see cref="InvalidDataException"/>; adding rtok's entry leaves it there and is idempotent, and
/// removing it leaves none and is idempotent.
/// </summary>
internal static class McpConfigText
{
    private static readonly McpSpec[] Specs =
    [
        Spec(McpFormat.Json, "mcpServers"),
        Spec(McpFormat.Jsonc, "mcpServers"),
        Spec(McpFormat.Toml, "mcp_servers"),
        Spec(McpFormat.Json, "mcp.servers"),
    ];

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        var spec = Specs[pick % Specs.Length];
        var entry = spec.Entry();
        try
        {
            _ = McpConfig.Servers(spec, text);
            var removed = McpConfig.Without(spec, text);
            FuzzInput.Check(McpConfig.Entry(spec, removed) is null, "an entry survives Without");
            var with = McpConfig.With(spec, text, (JsonObject)entry.DeepClone());
            FuzzInput.Check(
                string.Equals(
                    McpConfig.Entry(spec, with)?.ToJsonString(),
                    entry.ToJsonString(),
                    StringComparison.Ordinal
                ),
                "With does not hold the entry"
            );
            FuzzInput.Check(
                string.Equals(
                    McpConfig.With(spec, with, (JsonObject)entry.DeepClone()),
                    with,
                    StringComparison.Ordinal
                ),
                "With is not idempotent"
            );
            var without = McpConfig.Without(spec, with);
            FuzzInput.Check(
                McpConfig.Entry(spec, without) is null,
                "an entry survives Without after With"
            );
            FuzzInput.Check(
                string.Equals(McpConfig.Without(spec, without), without, StringComparison.Ordinal),
                "Without is not idempotent"
            );
        }
        catch (InvalidDataException) { }
    }

    private static McpSpec Spec(McpFormat format, string keyPath) =>
        new(
            "fuzz",
            "cli",
            McpClient.Cli,
            "/fuzz/.config/host/config",
            format,
            keyPath.Split('.'),
            McpServer.Rtok,
            new McpLaunch(HostArg: false),
            EntryShape.TypedCommand,
            DuplicateName.ShowsBoth
        );
}
