using System.IO.Abstractions;

namespace Rtok.Plugins.Cmd;

/// <summary><c>[plugins.cmd]</c> knobs shared by rule picking and <see cref="RuleEngine.Apply"/>.</summary>
internal sealed class CmdSettings
{
    private readonly List<Rule> _rules;

    private CmdSettings(List<Rule> rules, uint failTailLines)
    {
        _rules = rules;
        FailTailLines = (int)Math.Max(failTailLines, 1);
    }

    /// <summary>Lines kept from the end of a failed run's output.</summary>
    public int FailTailLines { get; }

    /// <summary>Built-in defaults only (golden tests and fail-open paths without config).</summary>
    public static CmdSettings Builtin() => new(RuleParser.Defaults(), 80);

    /// <summary>
    /// Built-ins, then the user rules file, then every <c>*.toml</c> in
    /// <paramref name="rulesDir"/> in name order; a later file wins per <c>match_cmd</c>. A
    /// missing or malformed file is no rules (fail open; <c>rtok config validate</c> reports it).
    /// </summary>
    public static CmdSettings Load(
        IFileSystem fs,
        string rulesPath,
        string? rulesDir,
        uint failTailLines
    )
    {
        var rules = RuleParser.Defaults();
        if (Read(fs, rulesPath) is { } user)
        {
            rules = Merge(rules, user);
        }

        if (rulesDir is not null)
        {
            foreach (var path in RuleParser.DropIns(fs, rulesDir))
            {
                if (Read(fs, path) is { } dropIn)
                {
                    rules = Merge(rules, dropIn);
                }
            }
        }

        return new(rules, failTailLines);
    }

    /// <summary>The rule whose <c>match_cmd</c> is <paramref name="bin"/>, else the default rule.</summary>
    public Rule Pick(string bin) =>
        _rules.Find(r => string.Equals(r.MatchCmd, bin, StringComparison.Ordinal)) ?? new Rule();

    internal static List<Rule> Merge(List<Rule> rules, List<Rule> user)
    {
        foreach (var rule in user)
        {
            var i = rules.FindIndex(r =>
                string.Equals(r.MatchCmd, rule.MatchCmd, StringComparison.Ordinal)
            );
            if (i >= 0)
            {
                rules[i] = rule;
            }
            else
            {
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static List<Rule>? Read(IFileSystem fs, string path)
    {
        try
        {
            return fs.File.Exists(path)
                ? RuleParser.ParseStrict(fs.File.ReadAllText(path), out _)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
