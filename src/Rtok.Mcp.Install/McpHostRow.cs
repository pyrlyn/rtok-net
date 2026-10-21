namespace Rtok.Mcp.Install;

/// <summary>One row of <see cref="McpHosts"/>: a host surface and how its spec is built.</summary>
internal sealed record McpHostRow(
    string Host,
    string Surface,
    McpClient Client,
    Func<McpHostPaths, string> Path,
    string Key,
    EntryShape Shape,
    bool Absolute = false,
    DuplicateName Duplicates = DuplicateName.ShowsBoth,
    string? PluginServes = null
)
{
    public McpSpec Spec(McpHostEnv env, McpHostPaths paths)
    {
        var path = env.Paths?.GetValueOrDefault($"{Host}.{Surface}") is { Length: > 0 } set
            ? set
            : Path(paths);
        var command = Absolute ? env.Exe ?? env.Command : env.Command;
        return new McpSpec(
            Host,
            Surface,
            Client,
            path,
            FormatOf(path, paths),
            // Amp's servers table is one flat key that holds a dot.
            Key.StartsWith("amp.", StringComparison.Ordinal)
                ? [Key]
                : Key.Split('.'),
            McpServer.Rtok,
            new McpLaunch(
                string.Equals(command, McpServer.Rtok.Command, StringComparison.Ordinal)
                    ? null
                    : command
            ),
            Shape,
            Duplicates,
            PluginServes
        );
    }

    private McpFormat FormatOf(string path, McpHostPaths paths) =>
        paths.Extension(path) switch
        {
            ".toml" => McpFormat.Toml,
            ".jsonc" => McpFormat.Jsonc,
            _ => Jsonc ? McpFormat.Jsonc : McpFormat.Json,
        };

    /// <summary>The host's own installer edits the file as JSONC, whatever its name.</summary>
    public bool Jsonc { get; init; }
}
