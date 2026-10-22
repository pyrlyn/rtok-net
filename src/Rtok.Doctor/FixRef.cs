namespace Rtok.Doctor;

/// <summary>One entry by its file and key path, as the web and TUI checklists name it.</summary>
/// <param name="Source">The file.</param>
/// <param name="Path">The key path in it.</param>
public sealed record FixRef(string Source, string Path);
