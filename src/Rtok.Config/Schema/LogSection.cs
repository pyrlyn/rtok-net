namespace Rtok.Config;

/// <summary>rtok's own log; `rtok logs` reads it</summary>
/// <remarks>Read from [log].</remarks>
public sealed class LogSection
{
    /// <summary>rotated siblings live beside it: rtok.log.1 … .5</summary>
    public string Path { get; set; } = "~/.rtok/logs/rtok.log";

    /// <summary>rotate past 1 MiB (validate: ≥ 1024)</summary>
    public ulong MaxBytes { get; set; } = 1048576;

    /// <summary>generations kept; older ones are deleted, never archived (validate: ≤ 20)</summary>
    public uint Files { get; set; } = 5;

    /// <summary>what `rtok logs` prints when --lines is not given</summary>
    public uint Lines { get; set; } = 200;

    /// <summary>error | warn | info | debug</summary>
    public string Level { get; set; } = "info";

    /// <summary>also write a `logs` row for `rtok otel`</summary>
    public bool ToDb { get; set; } = true;

    /// <summary>`rtok logs` through tailspin: auto = terminal and tspin on PATH | always | off</summary>
    public string Tspin { get; set; } = "auto";
}
