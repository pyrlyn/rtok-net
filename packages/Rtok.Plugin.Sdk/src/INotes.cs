namespace Rtok.Plugin.Sdk;

/// <summary>Durable notes the host can search: what a plugin remembers between
/// sessions.</summary>
public interface INotes
{
    /// <summary>Upsert on <c>(project, kind, title)</c> and return the note id.</summary>
    long UpsertNote(string? project, string kind, string title, string body);

    /// <summary>Save a note and return its id; <paramref name="project"/>
    /// <see langword="null"/> means not project-bound. Same one-row-per-key contract as
    /// <see cref="UpsertNote"/>: a repeat key updates the row.</summary>
    long InsertNote(string? project, string kind, string title, string body) =>
        UpsertNote(project, kind, title, body);

    /// <summary>The body of the most recent note of <paramref name="kind"/>.</summary>
    string? LatestNote(string kind);

    /// <summary>Newest note body whose kind starts with <paramref name="kindPrefix"/> for
    /// <paramref name="project"/>.</summary>
    string? LatestNoteForProject(string? project, string kindPrefix);

    /// <summary>Newest <c>session:*</c> note body for <paramref name="project"/>.</summary>
    string? LatestSessionNote(string? project) => null;

    /// <summary>The <paramref name="limit"/> most recent <c>(id, title)</c> pairs, newest
    /// first.</summary>
    IReadOnlyList<(long Id, string Title)> ListNoteTitles(string? project, int limit);

    /// <summary>One note's body by id.</summary>
    string? GetNoteBody(long id);

    /// <summary>Full-text search over notes. An empty query returns no hits rather than
    /// everything.</summary>
    IReadOnlyList<NoteHit> SearchNotes(string query, int limit);

    /// <summary>Full-text search fused with stored vectors. Does not embed missing rows; the
    /// default is <see cref="SearchNotes"/>.</summary>
    IReadOnlyList<NoteHit> SearchNotesHybridStored(string query, int limit) =>
        SearchNotes(query, limit);

    /// <summary>Replace the files note <paramref name="id"/> is linked to; paths are relative to
    /// the project root.</summary>
    void SetNoteFiles(long id, IReadOnlyList<string> paths) { }

    /// <summary>Live notes of <paramref name="project"/> (and unbound ones) linked to any of
    /// <paramref name="paths"/>, the one linked to most of them first.</summary>
    IReadOnlyList<NoteHit> NotesForFiles(string? project, IReadOnlyList<string> paths, int limit) =>
        [];

    /// <summary>Count one read of note <paramref name="id"/>.</summary>
    void TouchNote(long id) { }

    /// <summary>Insert a mechanical observation; <see langword="null"/> means a recent
    /// duplicate was skipped or the host stores none.</summary>
    long? InsertObservation(NewObservation observation) => null;

    /// <summary>Full-text search over observation narratives. An empty query returns no
    /// hits.</summary>
    IReadOnlyList<ObsHit> SearchObservations(string? project, string query, int limit) => [];

    /// <summary>Observations linked to any of <paramref name="paths"/>, newest first.</summary>
    IReadOnlyList<ObsHit> ObservationsForFiles(
        string? project,
        IReadOnlyList<string> paths,
        int limit
    ) => [];

    /// <summary>Newest observations in one session.</summary>
    IReadOnlyList<ObsHit> RecentObservations(string sessionId, int limit) => [];

    /// <summary>The scrubbed narrative of observation <paramref name="id"/>.</summary>
    string? ObservationNarrative(long id) => null;
}
