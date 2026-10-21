namespace Rtok.Store.Model;

/// <summary>Table <c>notes</c>: a memory note; never deleted, only retired.</summary>
public class Note
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string? Project { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public long? Retired { get; set; }
    public long? SupersededBy { get; set; }
    public bool Pinned { get; set; }
    public long Uses { get; set; }
    public long? LastUsed { get; set; }
}
