using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// Files that change together in git history (rtok <c>cochange.rs</c>): a migration and its
/// model, a test and its fixture share no name the symbol graph could see, but they share
/// commits. Pairs are counted from the last commits, kept per root and recounted only when HEAD
/// moves, so a repeat <c>impact</c> costs one HEAD read.
/// </summary>
internal static class Cochange
{
    /// <summary>Commits read. A longer window mostly finds files that no longer exist.</summary>
    private const int Commits = 300;

    /// <summary>A commit touching more files is a rename, a format run or a vendor drop.</summary>
    private const int MaxFiles = 20;

    /// <summary>One shared commit is chance.</summary>
    private const int MinCount = 2;

    /// <summary>Partners listed after an <c>impact</c> answer.</summary>
    private const int TopPartners = 5;

    private static string Key(string root) => "cochange:" + root;

    /// <summary>The pairs of the canonical <paramref name="root"/>, most shared commits first;
    /// empty without git history.</summary>
    public static IReadOnlyList<CochangePair> Pairs(Ctx cx, string root)
    {
        if (Git.Head(root) is not { } head)
        {
            return [];
        }

        if (Stored(cx, root) is { } doc && string.Equals(doc.Head, head, StringComparison.Ordinal))
        {
            return doc.Pairs;
        }

        if (Git.CommitFiles(root, Commits) is not { } commits)
        {
            return [];
        }

        var pairs = Count(commits);
        cx.Host.PluginStateSet(
            "graph",
            Key(root),
            System.Text.Json.JsonSerializer.Serialize(
                new CochangeDoc(head, pairs),
                GraphJson.Default.CochangeDoc
            )
        );
        return pairs;
    }

    /// <summary><c>changes with: a.rs (7), b.rs (4)</c> for the files that most often change
    /// with <paramref name="file"/>, or <see langword="null"/>. A partner no longer on disk is
    /// left out.</summary>
    public static string? ChangesWith(Ctx cx, string root, string file)
    {
        var partners = Partners(Pairs(cx, root), file)
            .Where(p => File.Exists(Path.Join(root, p.Other)))
            .Take(TopPartners)
            .Select(p => $"{p.Other} ({p.Count})")
            .ToList();
        return partners.Count == 0 ? null : "changes with: " + string.Join(", ", partners);
    }

    /// <summary>Pair counts from each commit's files, by count, then path, so the same history
    /// gives the same pairs. A commit with one file, none (a merge) or over
    /// <see cref="MaxFiles"/> is dropped.</summary>
    internal static List<CochangePair> Count(IEnumerable<IEnumerable<string>> commits)
    {
        var counts = new Dictionary<(string, string), int>();
        foreach (var files in Distinct(commits))
        {
            for (var i = 0; i < files.Count; i++)
            {
                for (var j = i + 1; j < files.Count; j++)
                {
                    var pair = (files[i], files[j]);
                    counts[pair] = counts.GetValueOrDefault(pair) + 1;
                }
            }
        }

        return
        [
            .. counts
                .Where(c => c.Value >= MinCount)
                .Select(c => new CochangePair(c.Key.Item1, c.Key.Item2, c.Value))
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.A, StringComparer.Ordinal)
                .ThenBy(p => p.B, StringComparer.Ordinal),
        ];
    }

    private static CochangeDoc? Stored(Ctx cx, string root)
    {
        if (cx.Host.PluginStateGet("graph", Key(root)) is not { } json)
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(json, GraphJson.Default.CochangeDoc);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<List<string>> Distinct(IEnumerable<IEnumerable<string>> commits) =>
        commits
            .Select(files =>
                files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
            )
            .Where(files => files.Count is >= 2 and <= MaxFiles);

    private static IEnumerable<(string Other, int Count)> Partners(
        IReadOnlyList<CochangePair> pairs,
        string file
    ) =>
        pairs
            .Where(p =>
                string.Equals(p.A, file, StringComparison.Ordinal)
                || string.Equals(p.B, file, StringComparison.Ordinal)
            )
            .Select(p => (string.Equals(p.A, file, StringComparison.Ordinal) ? p.B : p.A, p.Count));
}
