namespace Pyrlyn.ChangePreview;

/// <summary>The sums the closing lines print.</summary>
/// <param name="FilesChanged">Edited files.</param>
/// <param name="Insertions">Lines added over all edits.</param>
/// <param name="Deletions">Lines removed over all edits.</param>
/// <param name="PathsRemoved">Removed paths.</param>
/// <param name="FilesRemoved">Files under the removed paths.</param>
/// <param name="BytesRemoved">Bytes under the removed paths.</param>
public sealed record PreviewTotals(
    int FilesChanged,
    int Insertions,
    int Deletions,
    int PathsRemoved,
    long FilesRemoved,
    long BytesRemoved
);
