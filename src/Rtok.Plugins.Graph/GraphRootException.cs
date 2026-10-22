namespace Rtok.Plugins.Graph;

/// <summary>A path the graph refuses as a project root: <c>/</c>, the home directory, or no
/// directory at all.</summary>
public sealed class GraphRootException : Exception
{
    /// <summary>A refusal without a root.</summary>
    public GraphRootException()
        : base("no project root") { }

    /// <summary>Refuses <paramref name="root"/>.</summary>
    public GraphRootException(string root)
        : base(Text(root)) => Root = root;

    /// <summary>Refuses <paramref name="root"/> for <paramref name="inner"/>.</summary>
    public GraphRootException(string root, Exception inner)
        : base(Text(root), inner) => Root = root;

    /// <summary>The refused path.</summary>
    public string Root { get; } = "";

    private static string Text(string root) =>
        $"no project root: {root} (pass a project directory)";
}
