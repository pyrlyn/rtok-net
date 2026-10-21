namespace Rtok.Agents.Hosts;

/// <summary>The spellings of <see cref="HostKind"/>.</summary>
public static class HostKinds
{
    /// <summary>The block header word: <c>CLI: Codex</c>, <c>Desktop: Claude Desktop</c>.</summary>
    public static string Label(this HostKind kind) => kind == HostKind.Cli ? "CLI" : "Desktop";

    /// <summary>The flag spelling (<c>--cli</c>, <c>--desktop</c>) and the word in <c>--json</c>.</summary>
    public static string Word(this HostKind kind) => kind == HostKind.Cli ? "cli" : "desktop";

    /// <summary>
    /// Whether <paramref name="kind"/> is wanted given <c>--cli</c>, <c>--desktop</c> and
    /// <c>--all</c>: no flag, or <c>--all</c>, means every variant.
    /// </summary>
    public static bool Wanted(this HostKind kind, bool cli, bool desktop, bool all) =>
        all || (!cli && !desktop) || (kind == HostKind.Cli ? cli : desktop);
}
