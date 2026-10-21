namespace Rtok.Config;

/// <summary>`rtok memory extract` turns a transcript into notes through a program of the user's choosing</summary>
/// <remarks>Read from [plugins.memory.extract].</remarks>
public sealed class MemoryExtractSection
{
    /// <summary>rtok memory extract: off by default, a model call can cost money</summary>
    public bool Enabled { get; set; }

    /// <summary>program reading the transcript on stdin, one {"title","body"} JSON line out per note</summary>
    public IList<string> Command { get; set; } = [];

    /// <summary>notes kept per run</summary>
    public uint MaxNotes { get; set; } = 5;

    /// <summary>transcript bytes sent to the program; the end of the session is kept</summary>
    public uint MaxInputBytes { get; set; } = 60000;
}
