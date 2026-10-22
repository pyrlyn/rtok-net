using System.IO.Abstractions;
using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Mcp.Install;
using Rtok.Testing;

namespace Rtok.Doctor.Tests;

/// <summary>
/// A sandboxed home on an in-memory disk for one doctor run: host files are written into it,
/// MCP and health probes answer from tables, and writes land on the same disk.
/// </summary>
internal sealed class DoctorFake
{
    private readonly Dictionary<string, string> _vars = new(StringComparer.Ordinal);

    public DoctorFake(McpOs os = McpOs.Linux)
        : this(
            os,
            os switch
            {
                McpOs.Windows => (MemFs.Windows(), @"C:\Users\u"),
                McpOs.MacOS => (MemFs.MacOS(), "/Users/u"),
                _ => (MemFs.Linux(), "/home/u"),
            }
        ) { }

    /// <summary>A home on the real disk under <paramref name="root"/>, for what only a real disk shows.</summary>
    public DoctorFake(TempDir root)
        : this(
            OperatingSystem.IsWindows() ? McpOs.Windows : McpOs.Linux,
            (new FileSystem(), Path.Combine(root.Path, "home"))
        ) { }

    private DoctorFake(McpOs os, (IFileSystem Fs, string Home) at)
    {
        Os = os;
        (Fs, Home) = at;
        Bin = Fs.Path.Combine(Home, "bin");
        Fs.Directory.CreateDirectory(Bin);
        Cwd = Fs.Path.Combine(Home, "proj");
        Fs.Directory.CreateDirectory(Cwd);
        _vars["PATH"] = Bin;
        _vars[HostEnvironment.SandboxVariable] = "1";
        if (os == McpOs.Windows)
        {
            _vars["PATHEXT"] = ".COM;.EXE;.BAT;.CMD";
            _vars["APPDATA"] = Fs.Path.Combine(Home, "AppData", "Roaming");
            _vars["LOCALAPPDATA"] = Fs.Path.Combine(Home, "AppData", "Local");
        }
    }

    public McpOs Os { get; }

    public IFileSystem Fs { get; }

    public string Home { get; }

    public string Bin { get; }

    public string Cwd { get; set; }

    public RtokConfig Config { get; } = new();

    /// <summary>The tool descriptions MCP servers answer with, by entry name; the others do not answer.</summary>
    public Dictionary<string, string[]> Tools { get; } = new(StringComparer.Ordinal);

    /// <summary>Health answers by <c>host:port</c>.</summary>
    public Dictionary<string, string> Health { get; } = new(StringComparer.Ordinal);

    /// <summary>The managed settings files; none by default.</summary>
    public List<string> Managed { get; } = [];

    /// <summary>Writes refused with this message.</summary>
    public string? FailWrites { get; set; }

    public string Self =>
        Os == McpOs.Windows ? @"C:\Program Files\rtok\rtok.exe" : "/opt/rtok/bin/rtok";

    public void Var(string name, string value) => _vars[name] = value;

    public DoctorOptions Options() =>
        new(Context(), Config, Cwd)
        {
            Managed = Managed,
            Mcp = (server, _) => Tools.GetValueOrDefault(server.Name),
            Health = (hostPort, _) => Health.GetValueOrDefault(hostPort),
            Write = (path, body) =>
            {
                if (FailWrites is { } why)
                {
                    throw new IOException(why);
                }
                var bak = Fs.Path.Combine(
                    Fs.Path.GetDirectoryName(path)!,
                    "_backup",
                    Fs.Path.GetFileName(path) + ".1"
                );
                Write(bak, Read(path) ?? "");
                Write(path, body);
                return bak;
            },
        };

    public HostContext Context() =>
        new(
            new HostEnvironment(Home, Os, v => _vars.GetValueOrDefault(v)),
            new McpDisk(Fs),
            (_, _, _) => ("1.0.0 (fake)", ""),
            new RtokBinary(Self),
            new HostSettings(DryRun: false, Yes: false, Backup: false),
            null
        );

    public DoctorReport Page() => DoctorPage.Page(Options());

    public string Text() => DoctorText.Render(Page());

    public void Write(string path, string text)
    {
        Fs.Directory.CreateDirectory(Fs.Path.GetDirectoryName(path)!);
        Fs.File.WriteAllText(path, text);
    }

    public string? Read(string path) => Fs.File.Exists(path) ? Fs.File.ReadAllText(path) : null;

    /// <summary>A path under the home from <c>/</c>-separated parts.</summary>
    public string At(string relative) =>
        Fs.Path.Combine([Home, .. relative.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>A path under the project from <c>/</c>-separated parts.</summary>
    public string InProject(string relative) =>
        Fs.Path.Combine([Cwd, .. relative.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>An executable on <c>PATH</c>.</summary>
    public string Exe(string name, string text = "")
    {
        var path = Fs.Path.Combine(Bin, name);
        Write(path, text);
        if (Os != McpOs.Windows)
        {
            Mode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return path;
    }

    /// <summary>Sets a file's Unix mode on the simulated disk, whatever the host OS.</summary>
    public void Mode(string path, UnixFileMode mode)
    {
#pragma warning disable CA1416 // MemFs simulates the OS of the fake, not the host's.
        Fs.File.SetUnixFileMode(path, mode);
#pragma warning restore CA1416
    }

    /// <summary>A hooks block with one command hook per (event, matcher, command).</summary>
    public static string Hooks(params (string Event, string? Matcher, string Command)[] hooks)
    {
        var events = hooks
            .GroupBy(h => h.Event, StringComparer.Ordinal)
            .Select(g =>
                $"\"{g.Key}\": ["
                + string.Join(
                    ", ",
                    g.Select(h =>
                        "{"
                        + (h.Matcher is null ? "" : $"\"matcher\": \"{h.Matcher}\", ")
                        + $"\"hooks\": [{{\"type\": \"command\", \"command\": {Quote(h.Command)}}}]}}"
                    )
                )
                + "]"
            );
        return "{\n  \"hooks\": {" + string.Join(", ", events) + "}\n}\n";
    }

    /// <summary>A JSON string literal.</summary>
    public static string Quote(string text) =>
        "\""
        + text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
        + "\"";

    /// <summary>Installs and enables a Claude plugin with <paramref name="hooks"/> as its <c>hooks/hooks.json</c>; returns its directory.</summary>
    public string Plugin(string id, string? hooks = null, string settings = "{}")
    {
        var dir = At($".claude/plugins/cache/{id.Split('@')[0]}");
        Fs.Directory.CreateDirectory(dir);
        if (hooks is not null)
        {
            Write(Fs.Path.Combine(dir, "hooks", "hooks.json"), hooks);
        }
        Write(
            At(".claude/plugins/installed_plugins.json"),
            "{\"plugins\": {" + Quote(id) + ": [{\"installPath\": " + Quote(dir) + "}]}}"
        );
        var path = At(".claude/settings.json");
        var current = Read(path) ?? settings;
        var at = current.IndexOf('{', StringComparison.Ordinal);
        var comma = current[(at + 1)..].Trim().StartsWith('}') ? "" : ",";
        Write(
            path,
            current[..(at + 1)]
                + $"\"enabledPlugins\": {{{Quote(id)}: true}}{comma}"
                + current[(at + 1)..]
        );
        return dir;
    }
}
