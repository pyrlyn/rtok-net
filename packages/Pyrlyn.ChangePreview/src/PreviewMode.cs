namespace Pyrlyn.ChangePreview;

/// <summary>How much of a preview to print.</summary>
public enum PreviewMode
{
    /// <summary>Every edit as a unified diff.</summary>
    Diff,

    /// <summary>One <c>path | 7 +++--</c> line per edit instead of its diff.</summary>
    Stat,
}
