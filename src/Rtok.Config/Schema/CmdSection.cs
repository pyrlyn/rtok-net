namespace Rtok.Config;

/// <summary>shell command output: `rtok run` and the Bash hook</summary>
/// <remarks>Read from [plugins.cmd].</remarks>
public sealed class CmdSection
{
    /// <summary>filter command output</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>PreToolUse(Bash) → `rtok run -- …`</summary>
    public bool Rewrite { get; set; } = true;

    /// <summary>"" = $SHELL</summary>
    public string Shell { get; set; } = "";

    /// <summary>extra filter rules; missing → built-in rules/default.toml</summary>
    public string Rules { get; set; } = "~/.rtok/rules.toml";

    /// <summary>drop-ins: every *.toml merges after rules in name order</summary>
    public string RulesDir { get; set; } = "~/.rtok/rules.d";

    /// <summary>print `[rtok &lt;id12&gt; · N lines]` after a cut; false = `--no-ref`</summary>
    public bool Trailer { get; set; } = true;

    /// <summary>add `[rtok &lt;id12&gt; · N lines]` above this</summary>
    public uint TrailerMinLines { get; set; } = 40;

    /// <summary>non-zero exit → last N lines verbatim</summary>
    public uint FailTailLines { get; set; } = 80;

    /// <summary>first-word deny list; heredocs, `&amp;` are always skipped</summary>
    public IList<string> NeverWrap { get; set; } = ["rtok", "sudo"];

    /// <summary>`-i` skips wrap only for these (docker: run/exec; kubectl: exec); `--interactive` always skips</summary>
    public IList<string> InteractiveStems { get; set; } =
    ["python", "node", "psql", "sqlite3", "irb", "bash", "sh", "zsh", "docker", "kubectl"];
}
