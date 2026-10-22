using Rtok.Config;

namespace Rtok.Measure;

/// <summary>
/// The config the fold reads: the <c>[plugins.archive]</c> replay, and the <c>read_whole</c>
/// thresholds from <c>[plugins.read]</c> and <c>[plugins.guard]</c>.
/// </summary>
public sealed record Replay
{
    public long KeepTurns { get; init; }
    public long MinTokens { get; init; }
    public int HeadLines { get; init; }
    public int TailLines { get; init; }

    /// <summary><c>[plugins.read] native_max_bytes</c>: the size above which the hook denies a native Read.</summary>
    public long ReadWholeMin { get; init; }

    /// <summary><c>[plugins.guard] window_turns</c>: an Edit this close after a Read needed its body.</summary>
    public long EditWindowTurns { get; init; }

    public static Replay From(RtokConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var a = cfg.Plugins.Archive;
        return new()
        {
            KeepTurns = a.KeepTurns,
            MinTokens = a.MinTokens,
            HeadLines = (int)Math.Min(a.HeadLines, int.MaxValue),
            TailLines = (int)Math.Min(a.TailLines, int.MaxValue),
            ReadWholeMin = (long)Math.Min(cfg.Plugins.Read.NativeMaxBytes, long.MaxValue),
            EditWindowTurns = cfg.Plugins.Guard.WindowTurns,
        };
    }
}
