namespace Rtok.Store.Model;

/// <summary>Table <c>note_versions</c>: a previous title and body of a note.</summary>
public class NoteVersion
{
    public long Id { get; set; }
    public long NoteId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public long Version { get; set; }
    public long Ts { get; set; }
}
