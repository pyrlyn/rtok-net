using Pyrlyn.FsKit;
using Rtok.Config;
using Rtok.Plugin.Sdk;
using Rtok.Plugins.Read;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The tags index of one root (rtok <c>graph/index.rs</c>). Freshness is git's rule: the same
/// mtime and size mean unchanged, nothing is opened; a different stat reads and hashes the
/// file, and the same sha only moves the stat. The walk and every write run on the calling
/// thread; reads and parses run on <see cref="ParseWorkers"/>.
/// </summary>
public static class Indexer
{
    /// <summary>Files per write transaction on a cold run: each commit is an fsync.</summary>
    internal const int BatchFiles = 200;

    /// <summary>The <c>file_sha</c> of a file that cannot be read, decoded or parsed; the
    /// stat gate skips it next time without opening it.</summary>
    internal const string EmptySha = "";

    /// <summary>The <c>[plugins.graph]</c> section of <paramref name="cx"/>.</summary>
    public static GraphSection Config(Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(cx);
        return cx.PluginConfig("graph", GraphJson.Default.GraphSection);
    }

    /// <summary>Indexes <paramref name="root"/> incrementally. A dry run walks and parses as a
    /// real one does and writes nothing; <paramref name="onCount"/> learns how many files pass
    /// the stat gate once the walk is done.</summary>
    /// <exception cref="GraphRootException"><paramref name="root"/> is <c>/</c>, the home
    /// directory or no directory.</exception>
    public static IndexReport Run(
        Ctx cx,
        string root,
        bool dryRun = false,
        Action<int>? onCount = null
    )
    {
        var config = Config(cx);
        var matcher = new Matcher(root, config);
        var key = matcher.Root;
        var symbols = cx.Host;
        var fingerprint = Extraction.Fingerprint(matcher.Extensions);
        var stale = !string.Equals(
            symbols.ExtractorFingerprint(key),
            fingerprint,
            StringComparison.Ordinal
        );
        if (stale && !dryRun)
        {
            symbols.DeleteSymbolsMissing(key, new HashSet<string>(StringComparer.Ordinal));
        }

        // A stale root is indexed cold, dry run included: its stats and shas are the old
        // extractor's, and a dry run must not skip what the real run is about to parse.
        var stats = stale
            ? new Dictionary<string, SymbolStat>(StringComparer.Ordinal)
            : symbols.SymbolStats(key);
        var report = new IndexReport();
        var keep = new HashSet<string>(StringComparer.Ordinal);
        var jobs = Gate(matcher, stats, report, keep);
        onCount?.Invoke(jobs.Count);
        var pending = new List<SymbolFile>();
        var touches = new List<(string Rel, (long Mtime, long Size) Stat)>();
        var replaced = false;
        ParseWorkers.Each(
            jobs,
            (job, parsed) =>
            {
                Stage(job, parsed, report, pending, touches);
                if (!dryRun && pending.Count >= BatchFiles)
                {
                    report.Inserted += symbols.ReplaceSymbolFiles(key, pending);
                    replaced = true;
                    pending.Clear();
                }
            }
        );
        if (dryRun)
        {
            report.Inserted += pending.Sum(f => f.Rows.Count);
            return report;
        }

        replaced |= pending.Count > 0;
        report.Inserted += symbols.ReplaceSymbolFiles(key, pending);
        foreach (var (rel, stat) in touches)
        {
            symbols.TouchSymbols(key, rel, stat.Mtime, stat.Size);
        }

        // A stored path the walk did not keep is gone; with none, nothing is deleted.
        replaced |= stats.Keys.Any(rel => !keep.Contains(rel));
        Finish(cx, key, keep, stale ? fingerprint : null, replaced || stale);
        return report;
    }

    // A full run's last writes: drop what the walk no longer yields, record a new extractor
    // fingerprint, the run's time, the idf table and the HEAD it indexed. A run that changed no
    // row (a warm one) skips the delete and the idf, which only follow the rows.
    private static void Finish(
        Ctx cx,
        string key,
        HashSet<string> keep,
        string? fingerprint,
        bool changed
    )
    {
        var symbols = cx.Host;
        if (changed)
        {
            symbols.DeleteSymbolsMissing(key, keep);
        }

        if (fingerprint is not null)
        {
            symbols.SetExtractorFingerprint(key, fingerprint);
        }

        symbols.TouchSymbolIndexedAt(key, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (changed)
        {
            symbols.RebuildSymbolIdf(key);
        }

        IndexedHead.Remember(cx, key);
    }

    /// <summary>Indexes only <paramref name="changed"/> event paths (absolute, or relative to
    /// the root). A vanished or no longer indexable path drops its rows; nothing else is
    /// removed.</summary>
    /// <exception cref="GraphRootException"><paramref name="root"/> is refused.</exception>
    public static IndexReport RunChanged(Ctx cx, string root, IEnumerable<string> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var matcher = new Matcher(root, Config(cx));
        var key = matcher.Root;
        var symbols = cx.Host;
        // Rows of a stale extractor are no evidence that a file is unchanged. The stored
        // fingerprint stays, so the next full run still goes cold.
        var stale = !string.Equals(
            symbols.ExtractorFingerprint(key),
            Extraction.Fingerprint(matcher.Extensions),
            StringComparison.Ordinal
        );
        var report = new IndexReport();
        var jobs = new List<ParseJob>();
        foreach (var path in changed.Distinct(StringComparer.Ordinal))
        {
            var abs = ChangedAbs(key, path);
            if (matcher.Relative(abs) is not { } rel)
            {
                continue;
            }

            if (!File.Exists(abs) || !matcher.Indexable(abs))
            {
                symbols.MarkSymbolsStale(Path.Join(key, rel));
                continue;
            }

            var stat = SymbolStat.Stamp(abs);
            var known = stale ? null : symbols.GetSymbolStat(key, rel);
            if (known is not null && (known.Mtime, known.Size) == stat && stat != (0, 0))
            {
                report.Skipped++;
                continue;
            }

            jobs.Add(new ParseJob(abs, rel, stat, known?.Sha, matcher.Extensions));
        }

        ParseWorkers.Each(
            jobs,
            (job, parsed) =>
            {
                var pending = new List<SymbolFile>(1);
                var touches = new List<(string, (long, long))>(1);
                Stage(job, parsed, report, pending, touches);
                report.Inserted += symbols.ReplaceSymbolFiles(key, pending);
                foreach (var (rel, (mtime, size)) in touches)
                {
                    symbols.TouchSymbols(key, rel, mtime, size);
                }
            }
        );
        symbols.RebuildSymbolIdf(key);
        return report;
    }

    /// <summary>Indexes <paramref name="root"/> only when it has no rows yet: the first tool
    /// call in a repository.</summary>
    /// <exception cref="GraphRootException"><paramref name="root"/> is refused.</exception>
    public static IndexReport Ensure(Ctx cx, string root)
    {
        ArgumentNullException.ThrowIfNull(cx);
        var key = GraphRoot.Check(root);
        return cx.Host.SymbolCount(key) > 0 ? new IndexReport() : Run(cx, key);
    }

    // The walk and the stat gate: every indexable file is kept; the ones whose stat moved
    // become jobs.
    private static List<ParseJob> Gate(
        Matcher matcher,
        IReadOnlyDictionary<string, SymbolStat> stats,
        IndexReport report,
        HashSet<string> keep
    )
    {
        var jobs = new List<ParseJob>();
        foreach (var entry in matcher.Walk())
        {
            if (entry.IsDirectory || entry.IsSymlink)
            {
                continue;
            }

            var path = entry.Path;
            var builtin = Outline.Supported(path);
            if (!matcher.Indexable(path))
            {
                report.ExcludeSkipped += builtin ? 1 : 0;
                continue;
            }

            if (matcher.UsesExtensionMap(path))
            {
                report.ExtensionMapped++;
            }
            else if (!builtin)
            {
                report.IncludeAdded++;
            }

            var rel = entry.RelativePath.Replace('\\', '/');
            keep.Add(rel);
            var stat = SymbolStat.Stamp(path);
            var known = stats.GetValueOrDefault(rel);
            // Same mtime and size: git's rule for "unchanged". Nothing is opened.
            if (known is not null && (known.Mtime, known.Size) == stat && stat != (0, 0))
            {
                report.Skipped++;
                continue;
            }

            jobs.Add(new ParseJob(path, rel, stat, known?.Sha, matcher.Extensions));
        }

        return jobs;
    }

    private static void Stage(
        ParseJob job,
        Parsed parsed,
        IndexReport report,
        List<SymbolFile> pending,
        List<(string, (long, long))> touches
    )
    {
        switch (parsed.Outcome)
        {
            // The unknown stat (0, 0) never passes the gate, so the file is read again once it
            // is readable, even when restoring access left its mtime and size alone.
            case ParseOutcome.Unreadable:
                pending.Add(new SymbolFile(job.Rel, EmptySha, 0, 0, []));
                break;
            case ParseOutcome.NotText:
                pending.Add(new SymbolFile(job.Rel, EmptySha, job.Stat.Mtime, job.Stat.Size, []));
                break;
            case ParseOutcome.Unparsed:
                report.Read++;
                pending.Add(new SymbolFile(job.Rel, EmptySha, job.Stat.Mtime, job.Stat.Size, []));
                break;
            case ParseOutcome.Same:
                report.Read++;
                report.Skipped++;
                touches.Add((job.Rel, job.Stat));
                break;
            default:
                report.Read++;
                report.Indexed++;
                pending.Add(
                    new SymbolFile(job.Rel, parsed.Sha, job.Stat.Mtime, job.Stat.Size, parsed.Rows!)
                );
                break;
        }
    }

    // An event path made absolute and canonical; a vanished file keeps its canonical parent.
    private static string ChangedAbs(string root, string path)
    {
        var raw = Path.GetFullPath(path, root);
        if (FsPath.TryCanonicalize(raw, out var real))
        {
            return real;
        }

        return
            Path.GetDirectoryName(raw) is { } parent
            && FsPath.TryCanonicalize(parent, out var realParent)
            ? Path.Join(realParent, Path.GetFileName(raw))
            : raw;
    }
}
