namespace Rtok.Process;

/// <summary>What a <see cref="ProgressUpdate"/>'s position and total count.</summary>
public enum ProgressUnit
{
    /// <summary>Things: objects, files, crates, runs.</summary>
    Items,

    /// <summary>A share from 0 to 100.</summary>
    Percent,

    /// <summary>Bytes transferred.</summary>
    Bytes,
}
