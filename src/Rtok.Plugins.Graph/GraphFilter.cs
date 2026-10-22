namespace Rtok.Plugins.Graph;

/// <summary>
/// The optional narrowing of a tool call: <paramref name="Path"/> is a substring of the file,
/// <paramref name="Kind"/> the exact definition kind (<c>symbol</c>, <c>outline</c>), and
/// <paramref name="All"/> lifts the <c>impact</c> budget. <c>impact</c> walks the whole graph
/// and filters the lines it reports, so depth still crosses files outside the path.
/// </summary>
public sealed record GraphFilter(string Path = "", string Kind = "", bool All = false)
{
    /// <summary>No narrowing.</summary>
    public static readonly GraphFilter None = new();

    internal bool PathOk(string path) =>
        Path.Length == 0 || path.Contains(Path, StringComparison.Ordinal);

    internal bool KindOk(string kind) =>
        Kind.Length == 0 || string.Equals(kind, Kind, StringComparison.Ordinal);

    /// <summary><c> in &lt;path&gt;</c> and <c> of kind &lt;kind&gt;</c> for an empty answer;
    /// nothing without a filter, so an unfiltered answer stays byte-exact.</summary>
    internal string ScopeNote() =>
        (Path.Length == 0 ? "" : " in " + Path) + (Kind.Length == 0 ? "" : " of kind " + Kind);
}
