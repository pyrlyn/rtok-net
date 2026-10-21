using System.Globalization;

namespace Rtok.Agent.Sdk;

/// <summary>
/// Undo copies of host files in a <c>_backup</c> folder beside them:
/// <c>_backup/&lt;name&gt;.bak-&lt;unix-seconds&gt;[-&lt;n&gt;]</c>. A copy is taken once per distinct
/// content and never overwritten; generations are capped per base name, the newest is never pruned.
/// </summary>
public static class Backups
{
    /// <summary>Folder name beside a host config that holds its undo copies.</summary>
    public const string Dir = "_backup";

    /// <summary>The default of <c>[setup] backup_files</c>.</summary>
    public const int DefaultKeep = 5;

    private const string Infix = ".bak-";

    /// <summary>
    /// Copy <paramref name="path"/> into <c>_backup/</c> beside it, then keep the newest
    /// <paramref name="keep"/> generations of that name (0 keeps all). <see langword="null"/> when
    /// there is no file yet, or when a generation of the same file already holds the same bytes:
    /// another file's copy never counts, since its own cap could prune it.
    /// </summary>
    public static string? Take(string path, int keep, TimeProvider? time = null)
    {
        var bak = Copy(path, time);
        if (bak is not null)
        {
            Prune(bak, keep);
        }
        return bak;
    }

    /// <summary><see cref="Take"/> without the prune: the copy a <see cref="BackupRun"/> holds until it knows whether the run changed anything.</summary>
    public static string? Copy(string path, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            return null;
        }
        var ts = (time ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds();
        return CopyAt(path, ts);
    }

    /// <summary>
    /// Delete the oldest generations beside <paramref name="kept"/>, a copy <see cref="Take"/>
    /// returned, until <paramref name="keep"/> remain. <paramref name="kept"/> itself is never
    /// deleted; 0 keeps all. Only regular files of that exact name shape in a <c>_backup</c>
    /// folder are candidates. Best effort: an fs error leaves the rest in place.
    /// </summary>
    public static void Prune(string kept, int keep)
    {
        ArgumentException.ThrowIfNullOrEmpty(kept);
        kept = Path.GetFullPath(kept);
        var dir = Path.GetDirectoryName(kept);
        var file = Path.GetFileName(kept);
        if (keep <= 0 || dir is null || !IsBackupDir(dir))
        {
            return;
        }
        var at = file.LastIndexOf(Infix, StringComparison.Ordinal);
        if (at < 0)
        {
            return;
        }
        var older = Generations(dir, file[..at])
            .Where(g => !string.Equals(g.Path, kept, StringComparison.Ordinal))
            .OrderBy(g => g.Ts)
            .ThenBy(g => g.N)
            .ToList();
        var excess = Math.Max(0, older.Count + 1 - keep);
        foreach (var g in older.Take(excess))
        {
            TryDelete(g.Path);
        }
    }

    /// <summary>
    /// The generations in the <c>_backup</c> folder <paramref name="dir"/> that a cap of
    /// <paramref name="keep"/> has no room for: per base name, all but the newest
    /// <paramref name="keep"/>, oldest first. Read-only, for a cap lowered after the copies were
    /// taken; 0 keeps all.
    /// </summary>
    public static IReadOnlyList<string> Stale(string dir, int keep)
    {
        ArgumentException.ThrowIfNullOrEmpty(dir);
        if (keep <= 0 || !IsBackupDir(dir) || !Directory.Exists(dir))
        {
            return [];
        }
        var names = Directory
            .EnumerateFiles(dir)
            .Select(Path.GetFileName)
            .Select(f =>
            {
                var at = f!.LastIndexOf(Infix, StringComparison.Ordinal);
                return at < 0 ? null : f[..at];
            })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var stale = new List<string>();
        foreach (var name in names)
        {
            var all = Generations(dir, name).OrderBy(g => g.Ts).ThenBy(g => g.N).ToList();
            stale.AddRange(all.Take(Math.Max(0, all.Count - keep)).Select(g => g.Path));
        }
        return stale;
    }

    /// <summary>The <c>_backup</c> folder beside <paramref name="path"/>.</summary>
    public static string DirFor(string path) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, Dir);

    internal static string? CopyAt(string path, long ts)
    {
        var name = Path.GetFileName(path);
        var body = File.ReadAllBytes(path);
        var dir = DirFor(path);
        if (IdenticalExists(dir, name, body))
        {
            return null;
        }
        Directory.CreateDirectory(dir);
        // Past the highest -<n> of this second, never a slot pruning freed: the new copy must
        // sort newest, or the next prune would delete it first.
        var n = Generations(dir, name)
            .Where(g => g.Ts == ts)
            .Select(g => g.N + 1)
            .DefaultIfEmpty(0)
            .Max();
        while (true)
        {
            var bak = Path.Combine(dir, n == 0 ? $"{name}{Infix}{ts}" : $"{name}{Infix}{ts}-{n}");
            try
            {
                using var stream = new FileStream(bak, FileMode.CreateNew, FileAccess.Write);
                stream.Write(body);
                return bak;
            }
            catch (Exception e)
                when (e is IOException or UnauthorizedAccessException && Path.Exists(bak))
            {
                n++;
            }
        }
    }

    private static bool IsBackupDir(string dir) =>
        string.Equals(
            Path.GetFileName(Path.TrimEndingDirectorySeparator(dir)),
            Dir,
            StringComparison.Ordinal
        );

    /// <summary>True when a generation of <paramref name="name"/> in <paramref name="dir"/> is byte-equal to <paramref name="body"/>. Size first.</summary>
    private static bool IdenticalExists(string dir, string name, byte[] body) =>
        Generations(dir, name)
            .Exists(g =>
            {
                try
                {
                    return new FileInfo(g.Path).Length == body.Length
                        && File.ReadAllBytes(g.Path).AsSpan().SequenceEqual(body);
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            });

    /// <summary>Regular files in <paramref name="dir"/> named <c>&lt;name&gt;.bak-&lt;ts&gt;[-&lt;n&gt;]</c>.</summary>
    private static List<Generation> Generations(string dir, string name)
    {
        var prefix = name + Infix;
        var found = new List<Generation>();
        if (!Directory.Exists(dir))
        {
            return found;
        }
        try
        {
            foreach (var info in new DirectoryInfo(dir).EnumerateFiles())
            {
                if (
                    info.LinkTarget is null
                    && info.Name.StartsWith(prefix, StringComparison.Ordinal)
                    && TryParseSuffix(info.Name[prefix.Length..], out var ts, out var n)
                )
                {
                    found.Add(new Generation(ts, n, info.FullName));
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return found;
    }

    /// <summary><c>&lt;ts&gt;</c> or <c>&lt;ts&gt;-&lt;n&gt;</c>, the suffix <see cref="Copy"/> writes; anything else is not a generation.</summary>
    private static bool TryParseSuffix(string suffix, out long ts, out long n)
    {
        var dash = suffix.IndexOf('-', StringComparison.Ordinal);
        var tsPart = dash < 0 ? suffix : suffix[..dash];
        var nPart = dash < 0 ? "0" : suffix[(dash + 1)..];
        n = 0;
        return Digits(tsPart, out ts) && Digits(nPart, out n);
    }

    private static bool Digits(string s, out long value)
    {
        value = 0;
        return s.Length > 0
            && s.All(char.IsAsciiDigit)
            && long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private readonly record struct Generation(long Ts, long N, string Path);
}
