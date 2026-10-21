namespace Pyrlyn.ChangePreview;

/// <summary>One line of a diff: its marker, its text with the line end, and where it starts in each side.</summary>
internal readonly record struct DiffLine(char Kind, string Text, int Old, int New);
