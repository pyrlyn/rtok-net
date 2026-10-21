using System.Collections.Frozen;
using Pyrlyn.TreeSitter.Tags;

namespace Pyrlyn.TreeSitter;

/// <summary>
/// A bundled grammar with its tags query: the upstream <c>tags.scm</c> and <c>locals.scm</c> of
/// the grammar's release plus rtok's extra patterns (imports, calls and type references the
/// upstream queries miss; Kotlin and C# queries of their own).
/// </summary>
public sealed class Grammar
{
    private readonly Lazy<TagsConfiguration> _tags;

    private Grammar(string id, Language language, string[] names, string tags, string locals)
    {
        Id = id;
        Language = language;
        Names = names;
        TagsQuery = tags;
        LocalsQuery = locals;
        // A failed compile is cached with the Lazy, so every caller sees the same exception.
        _tags = new Lazy<TagsConfiguration>(() => new TagsConfiguration(language, tags, locals));
    }

    /// <summary>The grammar id, such as <c>rust</c>, <c>tsx</c> or <c>c_sharp</c>.</summary>
    public string Id { get; }

    /// <summary>The parser.</summary>
    public Language Language { get; }

    /// <summary>File extensions (without the dot) and aliases that select this grammar.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The tags query text: upstream patterns, then rtok's.</summary>
    public string TagsQuery { get; }

    /// <summary>The locals query text, empty when the grammar uses none.</summary>
    public string LocalsQuery { get; }

    /// <summary>The compiled tags configuration, built on first use and kept for the process.</summary>
    /// <exception cref="QueryException">The bundled query does not compile.</exception>
    public TagsConfiguration Tags => _tags.Value;

    /// <summary>Every bundled grammar.</summary>
    public static IReadOnlyList<Grammar> All { get; } = Build();

    private static FrozenDictionary<string, Grammar> ByName { get; } = Index();

    /// <summary>
    /// The grammar for an id, extension or alias (<c>rs</c>, <c>rust</c>, <c>h</c>, <c>mjs</c>,
    /// …), compared ordinally; null when none matches.
    /// </summary>
    public static Grammar? Find(string idOrName)
    {
        ArgumentNullException.ThrowIfNull(idOrName);
        return ByName.GetValueOrDefault(idOrName);
    }

    /// <inheritdoc />
    public override string ToString() => Id;

    private static Grammar[] Build()
    {
        var jsImport = Read("javascript/import.scm");
        var tsTags = Read("typescript/tags.scm") + jsImport + Read("typescript/extra.scm");
        var tsLocals = Read("typescript/locals.scm");
        return
        [
            new(
                "rust",
                Language.Rust,
                ["rs", "rust"],
                Read("rust/tags.scm") + Read("rust/extra.scm"),
                ""
            ),
            new("typescript", Language.TypeScript, ["ts", "typescript"], tsTags, tsLocals),
            new("tsx", Language.Tsx, ["tsx"], tsTags, tsLocals),
            new(
                "javascript",
                Language.JavaScript,
                ["js", "mjs", "cjs", "javascript"],
                Read("javascript/tags.scm") + jsImport,
                Read("javascript/locals.scm")
            ),
            new(
                "python",
                Language.Python,
                ["py", "python"],
                Read("python/tags.scm") + Read("python/extra.scm"),
                ""
            ),
            new(
                "dart",
                Language.Dart,
                ["dart"],
                Read("dart/tags.scm") + Read("dart/extra.scm"),
                Read("dart/locals.scm")
            ),
            new("c", Language.C, ["c", "h"], Read("c/tags.scm"), ""),
            new("go", Language.Go, ["go"], Read("go/tags.scm") + Read("go/extra.scm"), ""),
            new("java", Language.Java, ["java"], Read("java/tags.scm"), ""),
            new("kotlin", Language.Kotlin, ["kt", "kts", "kotlin"], Read("kotlin/tags.scm"), ""),
            new("swift", Language.Swift, ["swift"], Read("swift/tags.scm"), ""),
            new(
                "c_sharp",
                Language.CSharp,
                ["cs", "csharp", "c_sharp"],
                Read("c_sharp/tags.scm"),
                ""
            ),
            new(
                "ruby",
                Language.Ruby,
                ["rb", "ruby"],
                Read("ruby/tags.scm"),
                Read("ruby/locals.scm")
            ),
            new("php", Language.Php, ["php"], Read("php/tags.scm"), ""),
        ];
    }

    private static FrozenDictionary<string, Grammar> Index()
    {
        var map = new Dictionary<string, Grammar>(StringComparer.Ordinal);
        foreach (var grammar in All)
        {
            map[grammar.Id] = grammar;
            foreach (var name in grammar.Names)
            {
                map[name] = grammar;
            }
        }
        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>A bundled query file, such as <c>rust/tags.scm</c>.</summary>
    internal static string Read(string path)
    {
        using var stream =
            typeof(Grammar).Assembly.GetManifestResourceStream("Pyrlyn.TreeSitter.Queries/" + path)
            ?? throw new InvalidOperationException($"Pyrlyn.TreeSitter: no bundled query {path}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
