using System.Text;
using Pyrlyn.TreeSitter;
using Pyrlyn.TreeSitter.Tags;

namespace Rtok.Plugins.Read;

/// <summary>
/// A file's shape without its bodies: <c>mode=map</c> and <c>mode=signatures</c> from the
/// grammar's tags query, a line scan for Markdown and <c>llms.txt</c>, and <c>mode=stripped</c>
/// from comment nodes. The graph plugin indexes the same <see cref="TagHit"/>s, so an outline
/// and the symbol index never disagree.
/// </summary>
public static class Outline
{
    private const int FallbackLines = 60;

    // A TagsContext holds a parser and a cursor and is not thread-safe; the graph indexer parses
    // on several workers.
    [ThreadStatic]
    private static TagsContext? s_context;

    /// <summary>The grammar for <paramref name="path"/>'s extension; an entry of
    /// <paramref name="extensions"/> (extension → grammar, <c>[plugins.graph.extensions]</c>)
    /// wins over the built-in names.</summary>
    public static Grammar? GrammarFor(
        string path,
        IReadOnlyDictionary<string, string>? extensions = null
    )
    {
        var ext = Extension(path);
        if (ext.Length == 0)
        {
            return null;
        }

        return extensions is not null && extensions.TryGetValue(ext, out var mapped)
            ? Grammar.Find(mapped)
            : Grammar.Find(ext);
    }

    /// <summary>A tags grammar exists for <paramref name="path"/>'s extension, built in or
    /// mapped. Cheap: nothing is parsed.</summary>
    public static bool Supported(
        string path,
        IReadOnlyDictionary<string, string>? extensions = null
    )
    {
        var ext = Extension(path);
        return ext.Length > 0
            && (
                Grammar.Find(ext) is not null
                || (
                    extensions is not null
                    && extensions.TryGetValue(ext, out var mapped)
                    && Grammar.Find(mapped) is not null
                )
            );
    }

    /// <summary>Definitions and references of <paramref name="utf8"/>; no grammar gives none.
    /// Imports come one row per named item with the full specifier.</summary>
    /// <exception cref="QueryException">The grammar's tags query does not compile.</exception>
    public static IReadOnlyList<TagHit> Tags(
        string path,
        ReadOnlyMemory<byte> utf8,
        IReadOnlyDictionary<string, string>? extensions = null
    )
    {
        if (GrammarFor(path, extensions) is not { } grammar)
        {
            return [];
        }

        var config = grammar.Tags;
        var result = (s_context ??= new TagsContext()).GenerateTags(config, utf8);
        var src = utf8.Span;
        var starts = LineStarts(src);
        var hits = new List<TagHit>(result.Tags.Count);
        foreach (var tag in result.Tags)
        {
            if (Slice(src, tag.NameRange) is not { } name)
            {
                continue;
            }

            var kind = config.SyntaxTypeName(tag.SyntaxTypeId);
            var lineText = Slice(src, tag.LineRange) ?? "";
            var line = tag.SpanStart.Row + 1;
            var endLine = LineOf(starts, Math.Max(tag.Range.End - 1, 0));
            var items = kind is "import" ? ImportSpec.Expand(name) : [("", name)];
            foreach (var (importPath, item) in items)
            {
                hits.Add(
                    new TagHit(
                        kind,
                        item,
                        line,
                        endLine,
                        tag.Range.Start,
                        tag.Range.End,
                        tag.IsDefinition,
                        lineText,
                        importPath
                    )
                );
            }
        }

        return DropBareImports(hits);
    }

    /// <summary><see cref="Tags(string, ReadOnlyMemory{byte}, IReadOnlyDictionary{string, string}?)"/>
    /// of a string.</summary>
    public static IReadOnlyList<TagHit> Tags(string path, string src) =>
        Tags(path, Encoding.UTF8.GetBytes(src));

    /// <summary>
    /// Definitions as <c>kw name line</c> under an <c>imports:</c> line (<c>map</c>), or the
    /// definition lines verbatim (<c>signatures</c>). Markdown gives its headings,
    /// <c>llms.txt</c> also its links; anything else the first 60 numbered lines.
    /// </summary>
    public static string Render(string path, string src, string mode)
    {
        ArgumentNullException.ThrowIfNull(src);
        var llms = Markdown.IsLlmsTxt(path);
        if (llms || Markdown.IsMarkdown(path))
        {
            return RenderMarkdown(src, mode, llms);
        }

        var hits = Tags(path, src);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var imports = hits.Where(h => h.Kind is "import" && !h.IsDef && seen.Add(h.Name))
            .Select(h => h.Name)
            .ToList();
        var defs = hits.Where(h => h.IsDef).ToList();
        if (defs.Count == 0 && imports.Count == 0)
        {
            return Fallback(src);
        }

        var rows = new List<string>();
        if (imports.Count > 0 && mode is not "signatures")
        {
            rows.Add("imports: " + string.Join(", ", imports));
        }

        foreach (var hit in defs)
        {
            rows.Add(
                mode is "signatures"
                    ? hit.LineText.TrimEnd()
                    : $"{FirstWord(hit.LineText) ?? hit.Kind} {hit.Name} {hit.Line}"
            );
        }

        return string.Join('\n', rows);
    }

    /// <summary>The source with comments removed and newlines kept, so line numbers still
    /// match; <see langword="null"/> without a grammar or a parse.</summary>
    public static string? Stripped(string path, string src)
    {
        ArgumentNullException.ThrowIfNull(src);
        var ext = Extension(path);
        if (ext.Length == 0 || Grammar.Find(ext) is not { } grammar)
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(src);
        using var parser = new Parser(grammar.Language);
        using var tree = parser.Parse(bytes);
        if (tree is null)
        {
            return null;
        }

        var drop = new bool[bytes.Length];
        var stack = new Stack<Node>();
        stack.Push(tree.Root);
        while (stack.TryPop(out var node))
        {
            if (node.Kind.Contains("comment", StringComparison.Ordinal))
            {
                var start = Math.Min(node.StartByte, drop.Length);
                var end = Math.Min(node.EndByte, drop.Length);
                drop.AsSpan(start, end - start).Fill(true);
                continue;
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        var kept = new List<byte>(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!drop[i] || bytes[i] == (byte)'\n')
            {
                kept.Add(bytes[i]);
            }
        }

        return Encoding.UTF8.GetString([.. kept]);
    }

    /// <summary>The extension of <paramref name="path"/> without the dot; empty for none and
    /// for a dotfile such as <c>.bashrc</c>.</summary>
    public static string Extension(string path)
    {
        var name = Path.GetFileName(path);
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? "" : name[(dot + 1)..];
    }

    /// <summary>The first 60 lines, numbered, and a note that the language is unknown.</summary>
    public static string Fallback(string src)
    {
        var body = string.Join(
            '\n',
            TextLines.Split(src).Take(FallbackLines).Select((l, i) => $"{i + 1}:{l}")
        );
        return $"{body}\n(note: unknown language, showing lines 1-{FallbackLines})";
    }

    private static string RenderMarkdown(string src, string mode, bool llms)
    {
        var headings = Markdown.Headings(src);
        var links = llms ? Markdown.LlmsLinks(src) : [];
        if (headings.Count == 0 && links.Count == 0)
        {
            return Fallback(src);
        }

        var text = headings.Count == 0 ? "" : Markdown.Render(headings, mode);
        if (links.Count > 0)
        {
            text = (text.Length == 0 ? "" : text + "\n") + Markdown.RenderLinks(links);
        }

        return text;
    }

    // The bare last-segment capture keeps that node an import; the full clause is the row kept.
    private static List<TagHit> DropBareImports(List<TagHit> hits)
    {
        var full = hits.Where(h => h.Kind is "import" && ImportSpec.IsFull(h.ImportPath))
            .Select(h => (h.Line, h.Name))
            .ToHashSet();
        hits.RemoveAll(h =>
            h.Kind is "import"
            && !ImportSpec.IsFull(h.ImportPath)
            && full.Contains((h.Line, h.Name))
        );
        return hits;
    }

    private static string? FirstWord(string text)
    {
        var words = text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? null : words[0];
    }

    private static string? Slice(ReadOnlySpan<byte> src, TextRange range) =>
        range.Start < 0 || range.End > src.Length || range.Start > range.End
            ? null
            : Encoding.UTF8.GetString(src[range.Start..range.End]);

    private static List<int> LineStarts(ReadOnlySpan<byte> src)
    {
        var starts = new List<int> { 0 };
        for (var i = src.IndexOf((byte)'\n'); i >= 0; )
        {
            starts.Add(i + 1);
            var next = src[(i + 1)..].IndexOf((byte)'\n');
            i = next < 0 ? -1 : i + 1 + next;
        }

        return starts;
    }

    // 1-based line of byte b: how many line starts are at or before it.
    private static int LineOf(List<int> starts, int b)
    {
        var (lo, hi) = (0, starts.Count);
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (starts[mid] <= b)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return Math.Max(lo, 1);
    }
}
