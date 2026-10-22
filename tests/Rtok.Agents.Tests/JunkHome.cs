using Pyrlyn.FsKit;
using Rtok.Agents.Junk;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Agents.Tests;

/// <summary>
/// A fake home: Claude Code installed, rtok's files under <c>~/.rtok</c>, the Linux cache layout,
/// and the report and clear over it. Files are written 40 days old unless a test says otherwise.
/// </summary>
internal sealed class JunkHome : IDisposable
{
    private readonly TempDir _dir = new("rtok-junk-");
    private readonly Dictionary<string, string> _env = new(StringComparer.Ordinal);

    public JunkHome()
    {
        // Canonical, so /var → /private/var on macOS cannot colour a comparison.
        Home = FsPath.Canonicalize(_dir.Path);
        Config.Log.Path = Path.Combine(Home, ".rtok", "logs", "rtok.log");
        Config.Core.DbPath = Path.Combine(Home, ".rtok", "rtok.db");
        Config.Core.ArchiveDir = Path.Combine(Home, ".rtok", "archive");
    }

    public string Home { get; }

    public DateTime Now { get; } = DateTime.UtcNow;

    public DateTime Old => Now - TimeSpan.FromDays(40);

    public RtokConfig Config { get; } = new();

    public List<JunkHost> Hosts { get; } = [new("claude", true, [], ["claude"])];

    public IJunkStore? Store { get; set; }

    public JunkRoots Roots => new(Home, v => _env.GetValueOrDefault(v), "linux");

    public void Set(string variable, string value) => _env[variable] = value;

    /// <summary>A path under the home; nothing is created.</summary>
    public string this[string relative] =>
        Path.Combine(Home, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Writes <paramref name="bytes"/> bytes at <paramref name="relative"/>, last written at <paramref name="at"/> (40 days ago by default).</summary>
    public string File(string relative, DateTime? at = null, int bytes = 5000, string? body = null)
    {
        var path = this[relative];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, body ?? new string('x', bytes));
        System.IO.File.SetLastWriteTimeUtc(path, at ?? Old);
        return path;
    }

    /// <summary>A directory holding a valid <c>CACHEDIR.TAG</c> and one blob written at <paramref name="at"/>.</summary>
    public string Tagged(string relative, DateTime? at = null)
    {
        File(relative + "/CACHEDIR.TAG", at, body: CacheDirTag.Signature + "\n");
        File(relative + "/blob", at);
        return this[relative];
    }

    public JunkContext Context =>
        new(this["work"], JunkContext.DefaultIdle, Now, JunkContext.ScanLimit);

    public JunkReport Report() =>
        JunkScan.Build(
            new JunkOptions
            {
                Config = Config,
                RtokHome = this[".rtok"],
                Roots = Roots,
                Hosts = Hosts,
                Context = Context,
                Store = Store,
            }
        );

    public JunkView View => new() { Now = Now, Home = Home };

    public JunkClearOptions Options(
        JunkFilter filter,
        Func<string, bool>? running = null,
        Action<string>? trash = null
    ) =>
        new()
        {
            Config = Config,
            Report = Report(),
            Filter = filter,
            View = View,
            Protected = JunkClear.Protected(Config, Roots, Hosts.SelectMany(h => h.Markers)),
            Store = Store,
            Running = running ?? (_ => false),
            Trash = trash ?? (p => throw new InvalidOperationException($"no trash in tests: {p}")),
        };

    public JunkCleared Clear(
        JunkFilter filter,
        bool yes,
        Func<string, bool>? running = null,
        Action<string>? trash = null
    ) => JunkClear.Run(Options(filter, running, trash), yes);

    public void Dispose() => _dir.Dispose();
}
