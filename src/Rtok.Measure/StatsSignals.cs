namespace Rtok.Measure;

/// <summary>
/// What the report learns from other plugins: the <c>read</c> hook's deny reasons and outline
/// grammars, the <c>archive</c> line clip, the <c>cmd</c> filter kinds and wrapper peel. Each
/// defaults to "that plugin is not there", so its rows stay empty until the plugin lands.
/// </summary>
public sealed class StatsSignals
{
    /// <summary>True for a tool result that is the <c>read</c> hook's deny reason.</summary>
    public Func<string, bool> IsReadDeny { get; init; } = static _ => false;

    /// <summary>True for a native Read result that the <c>read</c> cache answered as unchanged.</summary>
    public Func<string, bool> IsDedup { get; init; } = static _ => false;

    /// <summary>True for a path <c>outline</c> has a grammar for.</summary>
    public Func<string, bool> HasOutline { get; init; } = static _ => false;

    /// <summary>A line's length as the archive pointer shows it (clipped), in UTF-8 bytes.</summary>
    public Func<string, long> ShownLength { get; init; } = static line => Transcript.Utf8(line);

    /// <summary>
    /// The <c>cmd</c> filter that handles a Bash family: <c>formatter</c>, a rule name or
    /// <c>default</c>; null when the <c>cmd</c> plugin is not there.
    /// </summary>
    public Func<string, string>? FilterKind { get; init; }

    /// <summary>The program a wrapper runs (<c>mise exec -- cargo</c>); null keeps argv[0].</summary>
    public Func<IReadOnlyList<string>, string?>? VisibleProgram { get; init; }

    /// <summary>No plugin signals: the generic markers only.</summary>
    public static StatsSignals None { get; } = new();
}
