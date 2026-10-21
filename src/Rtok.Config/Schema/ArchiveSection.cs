namespace Rtok.Config;

/// <summary>large tool results in proxied requests, archived for `rtok expand`</summary>
/// <remarks>Read from [plugins.archive].</remarks>
public sealed class ArchiveSection
{
    /// <summary>archive large tool results in proxied requests</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>never touch the last N turns</summary>
    public uint KeepTurns { get; set; } = 4;

    /// <summary>only rewrite tool results above this (estimated)</summary>
    public uint MinTokens { get; set; } = 1500;

    /// <summary>lines kept from the start of an archived result</summary>
    public uint HeadLines { get; set; } = 8;

    /// <summary>lines kept from the end of an archived result</summary>
    public uint TailLines { get; set; } = 4;

    /// <summary>optional L0/L1/L2 depth (OpenViking spec, AGPL-3.0 — rtok does not vendor/link/subprocess; native implementation)</summary>
    public bool Tiers { get; set; }

    /// <summary>shrink nested JSON dumps + data: blobs in user blocks, never results/system/tools/last-2-turns (opt-in)</summary>
    public bool LiveBlobs { get; set; }

    /// <summary>opt-in skill body archiving outside the live zone</summary>
    public bool Skills { get; set; }
}
