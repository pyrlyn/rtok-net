namespace Pyrlyn.ChangePreview;

/// <summary>One path a command would delete, with everything under it.</summary>
/// <param name="Path">The path as the preview prints it.</param>
/// <param name="Bytes">The bytes it takes.</param>
/// <param name="Files">The files under it (directories not counted).</param>
public sealed record Removal(string Path, long Bytes, long Files);
