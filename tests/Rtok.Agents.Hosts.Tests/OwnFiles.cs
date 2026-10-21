using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;
using Rtok.Testing;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>A host's own MCP file on an in-memory disk: write it, apply a mode, read it back.</summary>
internal sealed class OwnFiles
{
    public const string Dir = "/home/u/.host";

    private readonly McpDisk _disk = new(MemFs.Linux());

    public static bool IsRtok(string s) => new RtokBinary(null).Is(s);

    public string? Text(McpFile file) =>
        _disk.Fs.File.Exists(file.Path) ? _disk.Fs.File.ReadAllText(file.Path) : null;

    public void Put(McpFile file, string text)
    {
        _disk.Fs.Directory.CreateDirectory(_disk.Fs.Path.GetDirectoryName(file.Path)!);
        _disk.Fs.File.WriteAllText(file.Path, text);
    }

    public string Apply(McpFile file, McpMode mode, string command = "rtok", Apply? apply = null) =>
        file.Apply(mode, apply ?? new Apply(), _disk, IsRtok, command);

    public bool Installed(McpFile file) => file.Installed(_disk, IsRtok);
}
