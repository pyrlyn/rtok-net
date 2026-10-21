using ConsoleAppFramework;

namespace Pyrlyn.CommandLine.Docs.Tests;

/// <summary>
/// The sample's commands: a root handler, a group that has a handler of its own, nested
/// commands, arguments, aliases of commands and options, defaults, an enum and a hidden command.
/// </summary>
// ConsoleAppFramework registers instance methods; the sample's do nothing with the instance.
#pragma warning disable CA1822
public sealed class SampleCommands
{
    /// <summary>Shows the sample's state.</summary>
    /// <param name="dryRun">Print what would change.</param>
    [Command("")]
    public void Root(bool dryRun = false) => _ = dryRun;

    /// <summary>Shows the configuration.</summary>
    /// <param name="format">Output format.</param>
    [Command("config")]
    public void Config(SampleFormat format = SampleFormat.Text) => _ = format;

    /// <summary>Reads one key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="scope">-s, Where to look.</param>
    [Command("config get")]
    public void ConfigGet([Argument] string key, string scope = "user") => _ = (key, scope);

    /// <summary>Sets one key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value; the key is removed without one.</param>
    /// <param name="force">-f|--yes, Overwrite without asking.</param>
    [Command("config set|put")]
    public void ConfigSet(
        [Argument] string key,
        [Argument] string? value = null,
        bool force = false
    ) => _ = (key, value, force);

    /// <summary>Lists the worktrees.</summary>
    /// <param name="lines">-n|--limit, How many to show.</param>
    [Command("worktree list|ls")]
    public void WorktreeList(int lines = 10) => _ = lines;

    /// <summary>
    /// Removes worktrees.
    /// Their branches stay.
    /// </summary>
    /// <param name="paths">The worktrees.</param>
    /// <param name="since">Only those idle this long.</param>
    /// <param name="ratio">The share of files that may differ.</param>
    [Command("worktree remove|rm")]
    public void WorktreeRemove(
        [Argument] string[] paths,
        string since = "7d",
        double? ratio = null
    ) => _ = (paths, since, ratio);

    /// <summary>Dumps internals.</summary>
    [Hidden]
    [Command("debug")]
    public void Debug() { }
}
#pragma warning restore CA1822
