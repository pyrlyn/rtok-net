using Rtok.Agent.Sdk;

namespace Rtok.Mcp.Install.Tests;

/// <summary>Test surfaces and the binary check a host passes.</summary>
internal static class Specs
{
    public const string File = "/home/u/.config/host/config";

    public static McpSpec Of(McpFormat format, string keyPath = "mcpServers") =>
        new(
            "test",
            "cli",
            McpClient.Cli,
            File,
            format,
            keyPath.Split('.'),
            McpServer.Rtok,
            new McpLaunch(HostArg: false),
            EntryShape.TypedCommand,
            DuplicateName.ShowsBoth
        );

    /// <summary>Stands in for rtok's own check, which also knows <c>rtok.exe</c> and its running path.</summary>
    public static bool IsRtokBin(string s) =>
        string.Equals(Path.GetFileNameWithoutExtension(s), "rtok", StringComparison.Ordinal);

    public static string Run(McpSpec spec, McpMode mode, McpDisk disk, Apply? apply = null) =>
        McpInstaller.Apply(spec, mode, apply ?? new Apply(), disk, IsRtokBin);
}
