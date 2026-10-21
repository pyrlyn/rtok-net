namespace Rtok.Plugins.Cmd;

/// <summary>How repeated lines fold before the head/tail cut (rtok T64.2).</summary>
internal enum Dedupe
{
    /// <summary>Lines stay as they are.</summary>
    Off,

    /// <summary>A run of equal lines becomes <c>line (×N)</c>.</summary>
    Adjacent,

    /// <summary>Lines equal after masking times, ids and durations fold to the first.</summary>
    Normalized,
}
