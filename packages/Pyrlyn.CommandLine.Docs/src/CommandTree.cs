namespace Pyrlyn.CommandLine.Docs;

/// <summary>
/// The model as the completion scripts walk it: every path a user can type (aliases and the
/// groups that only prefix other commands included), its next words, and its options.
/// </summary>
internal sealed class CommandTree
{
    /// <summary>Options every ConsoleAppFramework command takes.</summary>
    internal static readonly OptionInfo[] Builtins =
    [
        new(["-h", "--help"], null, "Show help.", null, false),
        new(["--version"], null, "Show version.", null, false),
    ];

    private CommandTree(string name, List<Node> nodes)
    {
        Name = name;
        Nodes = nodes;
    }

    internal string Name { get; }

    /// <summary>Every path, root first, in the model's order; a group comes before its first command.</summary>
    internal IReadOnlyList<Node> Nodes { get; }

    internal static CommandTree From(CommandModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var nodes = new List<Node>();
        var byPath = new Dictionary<string, Node>(StringComparer.Ordinal);

        Node Get(string path)
        {
            if (byPath.TryGetValue(path, out var node))
            {
                return node;
            }
            node = new Node(path);
            if (path.Length != 0)
            {
                var space = path.LastIndexOf(' ');
                var parent = Get(space < 0 ? "" : path[..space]);
                parent.Children.Add(node);
            }
            byPath[path] = node;
            nodes.Add(node);
            return node;
        }

        foreach (var command in model.Commands)
        {
            foreach (var path in (string[])[command.Path, .. command.Aliases])
            {
                Get(path).Command = command;
            }
        }
        return new CommandTree(model.Name, nodes);
    }

    internal sealed class Node(string path)
    {
        internal string Path { get; } = path;

        /// <summary>The last word of the path; empty for the root.</summary>
        internal string Word => Path[(Path.LastIndexOf(' ') + 1)..];

        /// <summary>The command this path runs, or null for a group that only prefixes others.</summary>
        internal CommandInfo? Command { get; set; }

        internal List<Node> Children { get; } = [];

        /// <summary>The command's options plus the built-in ones; none for a bare group.</summary>
        internal IEnumerable<OptionInfo> Options =>
            Command is null ? [] : Command.Options.Concat(Builtins);

        /// <summary>The first line of the description, for a completion's hint.</summary>
        internal string Hint => Command is null ? "" : FirstLine(Command.Description);
    }

    internal static string FirstLine(string text)
    {
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        return newline < 0 ? text : text[..newline];
    }
}
