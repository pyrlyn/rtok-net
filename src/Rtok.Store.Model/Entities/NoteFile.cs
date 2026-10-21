namespace Rtok.Store.Model;

/// <summary>Table <c>note_files</c>: a path a note is about.</summary>
public class NoteFile
{
    public long NoteId { get; set; }
    public string Path { get; set; } = "";
}
