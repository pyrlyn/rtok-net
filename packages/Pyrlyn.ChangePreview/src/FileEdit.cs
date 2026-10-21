namespace Pyrlyn.ChangePreview;

/// <summary>One file a command would rewrite.</summary>
/// <param name="Path">The path as the preview prints it.</param>
/// <param name="Insertions">Lines added.</param>
/// <param name="Deletions">Lines removed.</param>
public sealed record FileEdit(string Path, int Insertions, int Deletions)
{
    /// <summary>The unified diff; not part of the JSON model.</summary>
    internal string Diff { get; init; } = "";
}
