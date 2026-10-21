namespace Rtok.Config;

/// <summary>the read, search and tree tools, and the native Read advice</summary>
/// <remarks>Read from [plugins.read].</remarks>
public sealed class ReadSection
{
    /// <summary>the read, search and tree tools</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>full | lines | map | signatures</summary>
    public string DefaultMode { get; set; } = "full";

    /// <summary>above this, head/tail + archive id</summary>
    public uint MaxChars { get; set; } = 20000;

    /// <summary>PreToolUse(Read) deny threshold; never below this</summary>
    public ulong NativeMaxBytes { get; set; } = 32768;

    /// <summary>a native Read with limit 1..=N passes the hook; 0 = unranged only</summary>
    public uint RangeMaxLines { get; set; } = 300;

    /// <summary>false = never deny native Read</summary>
    public bool Advice { get; set; } = true;

    /// <summary>extra roots outside cwd</summary>
    public IList<string> AllowPaths { get; set; } = [];

    /// <summary>search hit ceiling</summary>
    public uint SearchMax { get; set; } = 50;

    /// <summary>search skips files larger than this</summary>
    public ulong SearchMaxBytes { get; set; } = 1048576;

    /// <summary>tree depth when none is given</summary>
    public uint TreeDepth { get; set; } = 2;

    /// <summary>changed re-read → unified diff vs last archive</summary>
    public bool Delta { get; set; } = true;

    /// <summary>full file when the diff is not below this fraction</summary>
    public double DeltaMaxRatio { get; set; } = 0.6;

    /// <summary>Deprecated compatibility key</summary>
    public IList<string> Languages { get; set; } = [];
}
