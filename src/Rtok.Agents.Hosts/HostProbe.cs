using System.IO.Abstractions;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>Finds a host app on this machine and asks its version (rtok T426, T168, T280).</summary>
/// <param name="env">The home, OS and environment.</param>
/// <param name="fs">Where apps and binaries are looked up.</param>
/// <param name="run">Runs <c>&lt;bin&gt; --version</c>.</param>
public sealed class HostProbe(HostEnvironment env, IFileSystem fs, HostCommand run)
{
    /// <summary>
    /// How long one <c>--version</c> probe may run: <c>cursor --version</c> under an empty home
    /// with stdout on a pipe never prints and holds the pipe for minutes (rtok T280).
    /// </summary>
    public static readonly TimeSpan VersionLimit = TimeSpan.FromSeconds(3);

    private const int VersionWidth = 32;

    /// <summary>The environment probes read.</summary>
    public HostEnvironment Env => env;

    /// <summary>The file system probes read.</summary>
    public IFileSystem Fs => fs;

    /// <summary>
    /// Where the app is: a desktop bundle first, else the binary on <c>PATH</c>; null when
    /// neither exists. A config folder alone does not count: one left behind by an
    /// uninstalled app is no reason to write into it (rtok T426).
    /// </summary>
    public string? AppPath(HostVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return InstalledApps(variant).FirstOrDefault()
            ?? variant.Bins.Select(FindOnPath).FirstOrDefault(p => p is not null);
    }

    /// <summary>Each of <paramref name="variant"/>'s desktop bundles or binaries that exists, in table order.</summary>
    public IEnumerable<string> InstalledApps(HostVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return variant.Apps.Select(a => Installed(App(a))).OfType<string>();
    }

    /// <summary>Whether the app is installed.</summary>
    public bool Present(HostVariant variant) => AppPath(variant) is not null;

    /// <summary>
    /// The installed version: the first line of <c>&lt;bin&gt; --version</c> that looks like a
    /// version (npm wrappers print noise first), else its first non-empty line, at most 32
    /// characters; <c>-</c> when there is no binary or it says nothing.
    /// </summary>
    public string Version(HostVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        foreach (var bin in variant.Bins)
        {
            if (
                FindOnPath(bin) is not { } program
                || run(program, ["--version"], VersionLimit) is not { } output
            )
            {
                continue;
            }
            var text = string.IsNullOrWhiteSpace(output.Stdout) ? output.Stderr : output.Stdout;
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if ((lines.Find(LooksLikeVersion) ?? lines.FirstOrDefault()) is { } line)
            {
                return line.Length > VersionWidth ? line[..VersionWidth] : line;
            }
        }
        return "-";
    }

    /// <summary>
    /// The first file named <paramref name="bin"/> (on Windows, or <paramref name="bin"/> plus a
    /// <c>PATHEXT</c> extension) in a <c>PATH</c> directory. In a sandbox only directories
    /// under the home count.
    /// </summary>
    public string? FindOnPath(string bin)
    {
        ArgumentNullException.ThrowIfNull(bin);
        if (env.Variable("PATH") is not { Length: > 0 } path)
        {
            return null;
        }
        var names = Names(bin);
        var separator = env.Os == McpOs.Windows ? ';' : ':';
        foreach (var dir in path.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (env.Sandboxed && !Under(dir, env.Home))
            {
                continue;
            }
            foreach (var name in names)
            {
                var candidate = fs.Path.Combine(dir, name);
                if (fs.File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    /// <summary><c>~/x</c> and <c>$VAR/x</c> as a path on this machine; anything else unchanged.</summary>
    public string Expand(string spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.StartsWith("~/", StringComparison.Ordinal))
        {
            return Join(env.Home, spec[2..]);
        }
        if (spec.StartsWith('$'))
        {
            var slash = spec.IndexOf('/', StringComparison.Ordinal);
            var name = slash < 0 ? spec[1..] : spec[1..slash];
            var rest = slash < 0 ? "" : spec[(slash + 1)..];
            return env.Variable(name) is { Length: > 0 } root ? Join(root, rest) : spec;
        }
        return spec;
    }

    /// <summary>An app path expanded, and re-rooted under the home in a sandbox.</summary>
    private string App(string spec)
    {
        var path = Expand(spec);
        if (!env.Sandboxed || Under(path, env.Home))
        {
            return path;
        }
        // /Applications/X.app → <home>/Applications/X.app; a drive prefix is dropped.
        var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.EndsWith(':'));
        return fs.Path.Combine([env.Home, .. parts]);
    }

    /// <summary>
    /// <paramref name="path"/> when it exists (a file or an <c>.app</c> bundle). On Windows an
    /// extensionless path also matches <c>name.exe</c> or <c>name.cmd</c> beside it.
    /// </summary>
    private string? Installed(string path)
    {
        if (fs.File.Exists(path) || fs.Directory.Exists(path))
        {
            return path;
        }
        if (fs.Path.GetDirectoryName(path) is not { } dir)
        {
            return null;
        }
        return Names(fs.Path.GetFileName(path))
            .Skip(1)
            .Select(n => fs.Path.Combine(dir, n))
            .FirstOrDefault(fs.File.Exists);
    }

    /// <summary><paramref name="bin"/>, then on Windows <paramref name="bin"/> plus each <c>PATHEXT</c> extension it does not already end in.</summary>
    private List<string> Names(string bin)
    {
        List<string> names = [bin];
        if (env.Os != McpOs.Windows)
        {
            return names;
        }
        var exts = (env.Variable("PATHEXT") is { Length: > 0 } raw ? raw : ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToList();
        if (!exts.Exists(e => bin.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            names.AddRange(exts.Select(e => bin + e.ToLowerInvariant()));
        }
        return names;
    }

    private string Join(string root, string relative) =>
        fs.Path.Combine([root, .. relative.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    private bool Under(string path, string root)
    {
        var comparison =
            env.Os == McpOs.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var trimmed = root.TrimEnd('/', '\\');
        return string.Equals(path.TrimEnd('/', '\\'), trimmed, comparison)
            || (
                path.StartsWith(trimmed, comparison)
                && path.Length > trimmed.Length
                && path[trimmed.Length] is '/' or '\\'
            );
    }

    /// <summary>
    /// A digit, then later a dot, then later another digit: enough to tell <c>0.1.0 (fake)</c>
    /// or <c>git version 2.43.0</c> from <c>Package extraction took 1234ms</c>.
    /// </summary>
    internal static bool LooksLikeVersion(string line)
    {
        var digit = false;
        var dot = false;
        foreach (var c in line)
        {
            if (char.IsAsciiDigit(c))
            {
                if (dot)
                {
                    return true;
                }
                digit = true;
            }
            else if (c == '.' && digit)
            {
                dot = true;
            }
        }
        return false;
    }
}
