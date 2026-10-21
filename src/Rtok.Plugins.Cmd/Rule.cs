namespace Rtok.Plugins.Cmd;

/// <summary>One filter applied to captured command output.</summary>
internal sealed record Rule
{
    /// <summary>The program this rule is for (argv[0] stem); empty for the default rule.</summary>
    public string MatchCmd { get; init; } = "";

    public uint MaxLines { get; init; } = 40;

    public uint Head { get; init; } = 10;

    public uint Tail { get; init; } = 10;

    /// <summary>Lines matching any pattern are dropped unless a keep pattern matches.</summary>
    public IReadOnlyList<string> Drop { get; init; } = [];

    /// <summary>Lines matching any pattern survive drop and the cut.</summary>
    public IReadOnlyList<string> Keep { get; init; } = [];

    public Dedupe Dedupe { get; init; } = Dedupe.Adjacent;

    /// <summary>
    /// Fold runs of spaces inside each line to one before the cut (rtok T65.3). On for the
    /// default rule (columnar <c>docker ps</c>, <c>ps aux</c>); TOML rules keep it off unless they ask.
    /// </summary>
    public bool CollapseColumns { get; init; } = true;

    public Group Group { get; init; }

    /// <summary>First N array elements kept; the rest is <c>… +K more</c> (rtok T65.2).</summary>
    public uint JsonItems { get; init; } = 20;

    /// <summary>Strings longer than this are cut with their character length (rtok T65.2).</summary>
    public uint JsonString { get; init; } = 200;
}
