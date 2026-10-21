using Rtok.Mcp.Install;
using Rtok.Testing;
using Testably.Abstractions.Testing;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>
/// A sandboxed home on an in-memory disk with the path rules of <paramref name="os"/>: apps are
/// made present by creating their binary under <c>~/bin</c> or their bundle re-rooted under the
/// home, as the probe looks for them (rtok T280). Nothing on the machine is read or run.
/// </summary>
internal sealed class FakeHome
{
    private readonly Dictionary<string, string> _vars = new(StringComparer.Ordinal);

    public FakeHome(McpOs os = McpOs.Linux)
    {
        Os = os;
        (Fs, Home) = os switch
        {
            McpOs.Windows => (MemFs.Windows(), @"C:\Users\u"),
            McpOs.MacOS => (MemFs.MacOS(), "/Users/u"),
            _ => (MemFs.Linux(), "/home/u"),
        };
        Bin = Fs.Path.Combine(Home, "bin");
        Fs.Directory.CreateDirectory(Bin);
        _vars["PATH"] = Bin;
        _vars[HostEnvironment.SandboxVariable] = "1";
        if (os == McpOs.Windows)
        {
            _vars["APPDATA"] = Fs.Path.Combine(Home, "AppData", "Roaming");
            _vars["LOCALAPPDATA"] = Fs.Path.Combine(Home, "AppData", "Local");
        }
    }

    public McpOs Os { get; }

    public MockFileSystem Fs { get; }

    public string Home { get; }

    /// <summary>The one <c>PATH</c> directory.</summary>
    public string Bin { get; }

    /// <summary>The running rtok's path, which entries name on Windows when <c>rtok</c> is not on <c>PATH</c>.</summary>
    public string Self =>
        Os == McpOs.Windows ? @"C:\Program Files\rtok\rtok.exe" : "/opt/rtok/bin/rtok";

    public HostEnvironment Env => new(Home, Os, v => _vars.GetValueOrDefault(v));

    public HostContext Context(
        HostSettings? settings = null,
        IReadOnlyDictionary<string, string>? paths = null
    ) =>
        new(
            Env,
            new McpDisk(Fs),
            (_, _, _) => ("1.0.0 (fake)", ""),
            new RtokBinary(Self),
            settings ?? new HostSettings(DryRun: false, Yes: false, Backup: false),
            paths
        );

    public HostRun Run(HostSettings? settings = null) => new(Context(settings));

    /// <summary>Puts <c>rtok</c> itself on <c>PATH</c>.</summary>
    public void PutRtokOnPath() =>
        Touch(Fs.Path.Combine(Bin, Os == McpOs.Windows ? "rtok.exe" : "rtok"));

    /// <summary>Makes every app of every host present that can exist on this OS.</summary>
    public void InstallAll()
    {
        foreach (var host in AgentHosts.All)
        {
            foreach (var v in host.Variants)
            {
                Install(v);
            }
        }
    }

    /// <summary>Makes <paramref name="v"/> present; false when it has no build for this OS.</summary>
    public bool Install(HostVariant v)
    {
        if (v.Bins.Count > 0)
        {
            Touch(Fs.Path.Combine(Bin, Os == McpOs.Windows ? v.Bins[0] + ".exe" : v.Bins[0]));
            return true;
        }
        if (v.Apps.FirstOrDefault(Fits) is not { } app)
        {
            return false;
        }
        var parts = app.Replace("~/", "", StringComparison.Ordinal)
            .Replace("$LOCALAPPDATA/", @"AppData/Local/", StringComparison.Ordinal)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        Fs.Directory.CreateDirectory(Fs.Path.Combine([Home, .. parts]));
        return true;
    }

    public void Write(string path, string text)
    {
        Fs.Directory.CreateDirectory(Fs.Path.GetDirectoryName(path)!);
        Fs.File.WriteAllText(path, text);
    }

    public string? Read(string path) => Fs.File.Exists(path) ? Fs.File.ReadAllText(path) : null;

    /// <summary>A path under the home from <c>/</c>-separated parts.</summary>
    public string At(string relative) =>
        Fs.Path.Combine([Home, .. relative.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>Whether an app path is where this OS installs apps.</summary>
    private bool Fits(string app) =>
        Os switch
        {
            McpOs.MacOS => app.StartsWith("/Applications/", StringComparison.Ordinal)
                || app.StartsWith("~/Applications/", StringComparison.Ordinal),
            McpOs.Windows => app.StartsWith("$LOCALAPPDATA/", StringComparison.Ordinal),
            _ => app.StartsWith("/opt/", StringComparison.Ordinal),
        }
        || (
            app.StartsWith("~/", StringComparison.Ordinal)
            && !app.StartsWith("~/Applications/", StringComparison.Ordinal)
        );

    private void Touch(string path) => Write(path, "");
}
