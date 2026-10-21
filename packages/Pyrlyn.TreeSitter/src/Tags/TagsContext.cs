using System.Runtime.InteropServices;
using System.Text;

namespace Pyrlyn.TreeSitter.Tags;

/// <summary>
/// Extracts tags, a port of tree-sitter-tags 0.25's <c>generate_tags</c>. Holds a parser and a
/// query cursor, so it is not thread-safe: one context per thread.
/// </summary>
public sealed class TagsContext : IDisposable
{
    private const int MaxLineLength = 180;
    private const int CancellationCheckInterval = 100;

    private readonly Parser _parser = new();
    private readonly QueryCursor _cursor = new();

    /// <summary>The tags of UTF-8 <paramref name="source"/>.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> fired.</exception>
    public TagsResult GenerateTags(
        TagsConfiguration config,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();
        _parser.Language = config.Language;
        _parser.Reset();
        using var tree =
            _parser.Parse(source.Span)
            ?? throw new InvalidOperationException("tree-sitter returned no tree.");
        var root = tree.Root;
        var run = new Run(config, source.Span);
        var iterations = 0;
        foreach (var match in _cursor.Matches(config.Query, root, source))
        {
            if (++iterations >= CancellationCheckInterval)
            {
                iterations = 0;
                cancellationToken.ThrowIfCancellationRequested();
            }
            run.Add(match);
        }
        return new TagsResult(run.Finish(), root.HasError);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cursor.Dispose();
        _parser.Dispose();
    }

    // One generate_tags call. The queue keeps one entry per name range, sorted by (end, start);
    // a lower pattern index replaces a higher one. An entry leaves the queue once a later name
    // starts after it ends. Ignored entries hold no tag and are never emitted (upstream lets
    // them through when draining the queue at the end; no bundled query uses @ignore).
    private ref struct Run
    {
        private readonly TagsConfiguration _config;
        private readonly ReadOnlySpan<byte> _source;
        private readonly List<Scope> _scopes;
        private readonly List<Entry> _queue = [];
        private readonly List<Tag> _tags = [];
        private LineInfo? _prevLine;

        public Run(TagsConfiguration config, ReadOnlySpan<byte> source)
        {
            _config = config;
            _source = source;
            _scopes = [new Scope(0, source.Length, Inherits: false)];
        }

        public void Add(QueryMatch match)
        {
            var info = _config.Patterns[match.PatternIndex];
            if (match.PatternIndex < _config.TagsPatternIndex)
            {
                AddLocals(match, info);
                return;
            }

            var (
                nameNode,
                tagNode,
                docsAdjacentNode,
                docNodes,
                syntaxTypeId,
                isDefinition,
                isIgnored
            ) = ReadCaptures(match, info);
            if (nameNode is not { } name)
            {
                return;
            }
            var nameRange = new TextRange(name.StartByte, name.EndByte);
            Tag? tag = null;
            if (tagNode is { } node)
            {
                if (name.HasError || (info.NameMustBeNonLocal && IsLocal(nameRange)))
                {
                    return;
                }
                tag = MakeTag(
                    node,
                    name,
                    nameRange,
                    docNodes,
                    docsAdjacentNode,
                    info,
                    syntaxTypeId,
                    isDefinition
                );
            }
            else if (!isIgnored)
            {
                return;
            }
            Enqueue(new Entry(nameRange, tag, match.PatternIndex));
            Emit();
        }

        private readonly (
            Node? Name,
            Node? Tag,
            Node? DocsAdjacent,
            List<Node> Docs,
            int SyntaxTypeId,
            bool IsDefinition,
            bool IsIgnored
        ) ReadCaptures(QueryMatch match, TagsConfiguration.PatternInfo info)
        {
            Node? nameNode = null;
            Node? tagNode = null;
            Node? docsAdjacentNode = null;
            var docNodes = new List<Node>();
            var syntaxTypeId = 0;
            var isDefinition = false;
            var isIgnored = false;
            foreach (var capture in match.Captures)
            {
                var index = capture.Index;
                if (index == _config.IgnoreCapture)
                {
                    isIgnored = true;
                    nameNode = capture.Node;
                }
                if (index == info.DocsAdjacentCapture)
                {
                    docsAdjacentNode = capture.Node;
                }
                if (index == _config.NameCapture)
                {
                    nameNode = capture.Node;
                }
                else if (index == _config.DocCapture)
                {
                    docNodes.Add(capture.Node);
                }
                if (_config.CaptureSyntaxType[index] >= 0)
                {
                    tagNode = capture.Node;
                    syntaxTypeId = _config.CaptureSyntaxType[index];
                    isDefinition = _config.CaptureIsDefinition[index];
                }
            }
            return (
                nameNode,
                tagNode,
                docsAdjacentNode,
                docNodes,
                syntaxTypeId,
                isDefinition,
                isIgnored
            );
        }

        public readonly List<Tag> Finish()
        {
            foreach (var entry in _queue)
            {
                if (entry.Tag is { } tag)
                {
                    _tags.Add(tag);
                }
            }
            _queue.Clear();
            return _tags;
        }

        private readonly void AddLocals(QueryMatch match, TagsConfiguration.PatternInfo info)
        {
            foreach (var capture in match.Captures)
            {
                var start = capture.Node.StartByte;
                var end = capture.Node.EndByte;
                if (capture.Index == _config.LocalScopeCapture)
                {
                    _scopes.Add(new Scope(start, end, info.LocalScopeInherits));
                }
                else if (capture.Index == _config.LocalDefinitionCapture)
                {
                    for (var i = _scopes.Count - 1; i >= 0; i--)
                    {
                        if (_scopes[i].Start <= start && _scopes[i].End >= end)
                        {
                            _scopes[i].Defs.Add(new TextRange(start, end));
                            break;
                        }
                    }
                }
            }
        }

        private readonly bool IsLocal(TextRange name)
        {
            var text = Slice(name);
            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                var scope = _scopes[i];
                if (scope.Start > name.Start || scope.End < name.End)
                {
                    continue;
                }
                foreach (var def in scope.Defs)
                {
                    if (Slice(def).SequenceEqual(text))
                    {
                        return true;
                    }
                }
                if (!scope.Inherits)
                {
                    break;
                }
            }
            return false;
        }

        private Tag MakeTag(
            Node tagNode,
            Node name,
            TextRange nameRange,
            List<Node> docNodes,
            Node? docsAdjacentNode,
            TagsConfiguration.PatternInfo info,
            int syntaxTypeId,
            bool isDefinition
        )
        {
            var docs = Docs(docNodes, docsAdjacentNode, info);
            var range = new TextRange(
                Math.Min(tagNode.StartByte, nameRange.Start),
                Math.Max(tagNode.EndByte, nameRange.End)
            );
            var spanStart = name.StartPoint;
            var spanEnd = name.EndPoint;

            // Line text and UTF-16 columns; a tag on the same line as the previous one reuses
            // its line and continues counting from its end.
            var prevUtf16Column = 0;
            var prevUtf8Byte = nameRange.Start - spanStart.Column;
            TextRange lineRange;
            if (_prevLine is { } line && line.Position.Row == spanStart.Row)
            {
                if (line.Position.Column <= spanStart.Column)
                {
                    prevUtf8Byte = line.Utf8Byte;
                    prevUtf16Column = line.Utf16Column;
                }
                lineRange = line.LineRange;
            }
            else
            {
                lineRange = LineRange(_source, nameRange.Start, spanStart, MaxLineLength);
            }
            var utf16Start =
                prevUtf16Column
                + Encoding.UTF8.GetCharCount(_source[prevUtf8Byte..nameRange.Start]);
            var utf16End = utf16Start + Encoding.UTF8.GetCharCount(Slice(nameRange));
            _prevLine = new LineInfo(spanEnd, nameRange.End, utf16End, lineRange);

            return new Tag(
                range,
                nameRange,
                lineRange,
                spanStart,
                spanEnd,
                new TextRange(utf16Start, utf16End),
                docs,
                isDefinition,
                syntaxTypeId
            );
        }

        private readonly string? Docs(
            List<Node> docNodes,
            Node? docsAdjacentNode,
            TagsConfiguration.PatternInfo info
        )
        {
            // With #select-adjacent!, keep the trailing run of doc nodes that each end at most
            // one row above the next.
            var docsStart = 0;
            if (docsAdjacentNode is { } adjacent && docNodes.Count > 0)
            {
                docsStart = docNodes.Count;
                var startRow = adjacent.StartPoint.Row;
                while (docsStart > 0 && docNodes[docsStart - 1].EndPoint.Row + 1 >= startRow)
                {
                    docsStart--;
                    startRow = docNodes[docsStart].StartPoint.Row;
                }
            }
            string? docs = null;
            for (var i = docsStart; i < docNodes.Count; i++)
            {
                var bytes = docNodes[i].Text(_source);
                if (!System.Text.Unicode.Utf8.IsValid(bytes))
                {
                    continue;
                }
                var content = Encoding.UTF8.GetString(bytes);
                if (info.DocStrip is { } strip)
                {
                    content = strip.Replace(content, "");
                }
                docs = docs is null ? content : docs + "\n" + content;
            }
            return docs;
        }

        private readonly void Enqueue(Entry entry)
        {
            int lo = 0,
                hi = _queue.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) >>> 1;
                var cmp = Compare(_queue[mid].NameRange, entry.NameRange);
                if (cmp == 0)
                {
                    if (_queue[mid].PatternIndex > entry.PatternIndex)
                    {
                        _queue[mid] = entry;
                    }
                    return;
                }
                if (cmp < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            _queue.Insert(lo, entry);
        }

        private readonly void Emit()
        {
            while (_queue.Count > 1 && _queue[0].NameRange.End < _queue[^1].NameRange.Start)
            {
                if (_queue[0].Tag is { } tag)
                {
                    _tags.Add(tag);
                }
                _queue.RemoveAt(0);
            }
        }

        private readonly ReadOnlySpan<byte> Slice(TextRange range) =>
            _source[range.Start..range.End];

        private static int Compare(TextRange a, TextRange b) =>
            a.End != b.End ? a.End.CompareTo(b.End) : a.Start.CompareTo(b.Start);
    }

    // The name's line: leading ASCII whitespace skipped, cut at a newline, at maxLength bytes
    // or before an incomplete UTF-8 sequence, trailing ASCII whitespace trimmed.
    internal static TextRange LineRange(
        ReadOnlySpan<byte> text,
        int startByte,
        Point startPoint,
        int maxLength
    )
    {
        var lineStart = startByte - startPoint.Column;
        while (lineStart < text.Length && IsAsciiWhitespace(text[lineStart]))
        {
            lineStart++;
        }
        var window = text.Slice(lineStart, Math.Min(maxLength, text.Length - lineStart));
        var length = window.IndexOf((byte)'\n');
        if (length < 0)
        {
            length = ValidUtf8Prefix(window);
        }
        var lineEnd = lineStart + length;
        while (lineEnd > lineStart && IsAsciiWhitespace(text[lineEnd - 1]))
        {
            lineEnd--;
        }
        return new TextRange(lineStart, lineEnd);
    }

    private static int ValidUtf8Prefix(ReadOnlySpan<byte> bytes)
    {
        var i = 0;
        while (
            i < bytes.Length
            && System.Text.Rune.DecodeFromUtf8(bytes[i..], out _, out var consumed)
                == System.Buffers.OperationStatus.Done
        )
        {
            i += consumed;
        }
        return i;
    }

    private static bool IsAsciiWhitespace(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)'\n' or 0x0C or (byte)'\r';

    private sealed record Scope(int Start, int End, bool Inherits)
    {
        public List<TextRange> Defs { get; } = [];
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Entry(TextRange NameRange, Tag? Tag, int PatternIndex);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LineInfo(
        Point Position,
        int Utf8Byte,
        int Utf16Column,
        TextRange LineRange
    );
}
