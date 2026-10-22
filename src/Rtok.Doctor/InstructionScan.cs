using System.Text;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Doctor;

/// <summary>
/// The instructions every Claude Code session loads: the user and project CLAUDE.md and
/// AGENTS.md, what enabled plugins print at SessionStart, and known context injectors; each
/// with its token estimate, and the lines several of them repeat.
/// </summary>
internal static class InstructionScan
{
    private static readonly string[] Injectors =
    [
        "lean-ctx",
        "engram",
        "ponytail",
        "claude-mem",
        "token-optimizer",
        "caveman",
        "headroom",
    ];

    public static InstructionAudit Audit(DoctorHome home, HookScan hooks, ClaudeProbes claude)
    {
        var sources = new List<(string Name, string Path, string Text)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void File(string name, string path)
        {
            if (home.TryRead(path) is { } text && seen.Add(home.Canonical(path)))
            {
                sources.Add((name, path, text));
            }
        }
        var settings = home.PathOf(home.Config.Doctor.SettingsPath);
        File(
            "claude-user",
            home.Under(home.Fs.Path.GetDirectoryName(settings) ?? home.Home, "CLAUDE.md")
        );
        File("claude-project", home.Under(home.Project, "CLAUDE.md"));
        File("agents-project", home.Under(home.Project, "AGENTS.md"));
        foreach (var source in SessionStart(home, hooks))
        {
            sources.Add(source);
        }
        var configs =
            (claude.Settings?.ToJsonString() ?? "") + (claude.ClaudeJson?.ToJsonString() ?? "");
        foreach (var name in Injectors.Where(n => configs.Contains(n, StringComparison.Ordinal)))
        {
            var skill = home.InHome($".claude/skills/{name}/SKILL.md");
            if (home.Fs.File.Exists(skill))
            {
                File(name, skill);
            }
            else
            {
                sources.Add((name, $"mcp:{name}", ""));
            }
        }
        var warn = home.Config.Doctor.InstructionWarnTokens;
        var rows = sources
            .Select(s =>
            {
                var tokens = TokenEstimator.Estimate(s.Text, TextClass.Prose, claude.Rates);
                return new InstructionRow(s.Name, tokens, s.Path, tokens > warn);
            })
            .ToList();
        return new InstructionAudit(rows, Duplicates(sources));
    }

    /// <summary>
    /// The text enabled plugins print at SessionStart, read without running anything: a
    /// <c>cat &lt;file&gt;</c> hook gives the file, an <c>echo</c> hook its words.
    /// </summary>
    private static IEnumerable<(string Name, string Path, string Text)> SessionStart(
        DoctorHome home,
        HookScan hooks
    )
    {
        var check = new CommandCheck(home);
        foreach (
            var e in hooks.Entries.Where(e =>
                e.Source.Scope == Scope.Plugin
                && string.Equals(e.Event, "SessionStart", StringComparison.Ordinal)
            )
        )
        {
            var id =
                hooks
                    .Plugins.FirstOrDefault(p =>
                        string.Equals(p.Dir, e.Source.PluginRoot, StringComparison.Ordinal)
                    )
                    .Id
                ?? "plugin";
            if (
                check.ShellSyntax(e.Command) is not null
                || check.Words(e.Command) is not { Count: > 0 } all
            )
            {
                continue;
            }
            var words = all.Skip(1).Select(a => check.Expand(a, e.Source).Value).ToList();
            var text = CommandCheck.Base(all[0]) switch
            {
                "echo" => string.Join(' ', words),
                "cat" when words is [var file] => home.TryRead(file),
                _ => null,
            };
            if (text is not null)
            {
                yield return ($"plugin:{id}", e.Source.Path, text);
            }
        }
    }

    /// <summary>Trimmed lines of 40 bytes or more found in two or more sources.</summary>
    private static List<InstructionDuplicate> Duplicates(
        List<(string Name, string Path, string Text)> sources
    )
    {
        var where = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var (name, _, text) in sources)
        {
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (Encoding.UTF8.GetByteCount(line) < 40)
                {
                    continue;
                }
                if (!where.TryGetValue(line, out var names))
                {
                    where[line] = names = new SortedSet<string>(StringComparer.Ordinal);
                    order.Add(line);
                }
                names.Add(name);
            }
        }
        return
        [
            .. order
                .Where(l => where[l].Count > 1)
                .Select(l => new InstructionDuplicate(l.Length > 60 ? l[..60] : l, [.. where[l]])),
        ];
    }
}
