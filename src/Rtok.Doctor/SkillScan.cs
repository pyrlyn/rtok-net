using System.Text;
using System.Text.Json.Nodes;

namespace Rtok.Doctor;

/// <summary>
/// The skills hosts list on every request: one listing per skill directory with a
/// <c>SKILL.md</c> under each host's roots and every installed Claude plugin. Long descriptions,
/// big bodies and a skill one host lists twice are flagged.
/// </summary>
internal static class SkillScan
{
    /// <summary>Above this a description is truncated by the host anyway.</summary>
    public const int DescMax = 120;

    /// <summary>Above this a body belongs in <c>references/</c>.</summary>
    public const int BodyMax = 8192;

    /// <summary>The roots under the home, whether the project has one too, and the hosts that read them.</summary>
    private static readonly (string Root, bool Project, string[] Hosts)[] Roots =
    [
        (".claude/skills", true, ["claude"]),
        (".agents/skills", true, ["codex", "cursor", "gemini", "copilot"]),
        (".codex/skills", false, ["codex"]),
        (".cursor/skills", false, ["cursor"]),
        (".gemini/skills", false, ["gemini"]),
        (".copilot/skills", false, ["copilot"]),
    ];

    private sealed record Listing(
        string Name,
        string Source,
        string Dir,
        int Desc,
        string Body,
        string[] Hosts
    );

    public static SkillsAudit? Audit(DoctorHome home, string pluginIndex)
    {
        var listings = Listings(home, pluginIndex).ToList();
        if (listings.Count == 0)
        {
            return null;
        }
        var rows = listings
            .DistinctBy(l => (l.Source, l.Name))
            .Select(l => new SkillRow(
                l.Name,
                l.Source,
                l.Desc,
                Encoding.UTF8.GetByteCount(l.Body),
                l.Desc > DescMax,
                Encoding.UTF8.GetByteCount(l.Body) > BodyMax
            ))
            .OrderByDescending(r => r.BodyBytes)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
        var duplicates = Duplicates(home, listings);
        return new SkillsAudit(
            rows,
            rows.Sum(r => r.DescChars),
            duplicates.Count == 0 ? null : duplicates
        );
    }

    private static IEnumerable<Listing> Listings(DoctorHome home, string pluginIndex)
    {
        var roots = new List<(string Dir, string Source, string[] Hosts)>();
        foreach (var (root, project, hosts) in Roots)
        {
            roots.Add((home.InHome(root), "user", hosts));
            if (project && !string.Equals(home.Cwd, home.Home, StringComparison.Ordinal))
            {
                roots.Add((home.Under(home.Cwd, root), "project", hosts));
            }
        }
        if (DoctorHome.At(home.TryJson(pluginIndex), "plugins") is JsonObject plugins)
        {
            foreach (var (id, installs) in plugins)
            {
                if (
                    (installs as JsonArray)
                        ?.Select(i => DoctorHome.Str(DoctorHome.At(i, "installPath")))
                        .OfType<string>()
                        .FirstOrDefault() is
                    { } dir
                )
                {
                    roots.Add((home.Under(dir, "skills"), $"plugin:{id}", ["claude"]));
                }
            }
        }
        foreach (var (dir, source, hosts) in roots)
        {
            if (!home.Fs.Directory.Exists(dir))
            {
                continue;
            }
            foreach (
                var skill in home
                    .Fs.Directory.EnumerateDirectories(dir)
                    .Order(StringComparer.Ordinal)
            )
            {
                if (home.TryRead(home.Fs.Path.Combine(skill, "SKILL.md")) is { } text)
                {
                    var (desc, body) = Parse(text);
                    yield return new Listing(
                        home.Fs.Path.GetFileName(skill),
                        source,
                        skill,
                        desc,
                        body,
                        hosts
                    );
                }
            }
        }
    }

    /// <summary>The description's length in the front matter and the body after it.</summary>
    internal static (int Desc, string Body) Parse(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() is not "---")
        {
            return (0, text);
        }
        var desc = 0;
        var found = false;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line is "---")
            {
                return (desc, string.Join('\n', lines[(i + 1)..]));
            }
            if (!found && line.StartsWith("description:", StringComparison.Ordinal))
            {
                found = true;
                desc = line["description:".Length..].Trim().Trim('"').Length;
            }
        }
        return (desc, text);
    }

    /// <summary>Per host, a skill listed from more than one of its roots.</summary>
    private static List<SkillDuplicate> Duplicates(DoctorHome home, List<Listing> listings)
    {
        var found = new List<SkillDuplicate>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var host in listings.SelectMany(l => l.Hosts).Distinct(StringComparer.Ordinal))
        {
            foreach (
                var named in listings
                    .Where(l => l.Hosts.Contains(host))
                    .GroupBy(l => l.Name, StringComparer.Ordinal)
            )
            {
                var copies = named
                    .DistinctBy(l => home.Canonical(l.Dir), StringComparer.Ordinal)
                    .ToList();
                if (copies.Count < 2 || !reported.Add(string.Join('\0', copies.Select(c => c.Dir))))
                {
                    continue;
                }
                found.Add(
                    new SkillDuplicate(
                        named.Key,
                        [.. copies.Select(c => new SkillCopy(c.Source, c.Dir))],
                        copies.TrueForAll(c =>
                            string.Equals(c.Body, copies[0].Body, StringComparison.Ordinal)
                        ),
                        copies.Skip(1).Sum(c => c.Desc) / 4
                    )
                );
            }
        }
        return [.. found.OrderBy(d => d.Name, StringComparer.Ordinal)];
    }
}
