namespace Rtok.Plugin.Sdk;

/// <summary>One full-text search hit from <see cref="INotes.SearchNotes"/>.</summary>
/// <param name="Id">Note id; pass it to <see cref="INotes.GetNoteBody"/> for the whole
/// note.</param>
/// <param name="Title">The note's title.</param>
/// <param name="Snippet">A short excerpt around the match, capped by the host; never the whole
/// body.</param>
public sealed record NoteHit(long Id, string Title, string Snippet);
