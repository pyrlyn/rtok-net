namespace Rtok.Store.Model;

/// <summary>Table <c>note_embeddings</c>: a vector of a note's text.</summary>
public class NoteEmbedding
{
    public long NoteId { get; set; }
    public string Model { get; set; } = "";
    public long Dims { get; set; }
    public string TextHash { get; set; } = "";
    public long EmbeddedAt { get; set; }
    public byte[] Vector { get; set; } = [];
}
