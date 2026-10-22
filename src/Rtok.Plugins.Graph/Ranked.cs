namespace Rtok.Plugins.Graph;

/// <summary>How one name resolved: the files whose references reach the winning definition
/// (<see langword="null"/> keeps every reference), the definitions it beat, and its file.</summary>
internal sealed record Ranked(IReadOnlySet<string>? Files, int Others, string? DefPath)
{
    /// <summary>Unambiguous, too common, or nothing resolved: every reference stays.</summary>
    public static readonly Ranked Unresolved = new(null, 0, null);

    public bool Allows(string path) => Files is null || Files.Contains(path);
}
