using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk;
using Rtok.Plugins.Read;

namespace Rtok.Plugins.Graph;

/// <summary>
/// Definitions as <c>symbol</c> and <c>explore</c> print them: the id and
/// <c>path:line kind</c> head, then the source, at most <c>body_lines</c> lines with the uncut
/// span archived, then the names it calls. Each source file is read once per answer, and a
/// file whose bytes no longer match the index prints <c>stale &lt;path&gt;</c> instead of a
/// slice of the wrong text.
/// </summary>
internal static class Bodies
{
    /// <summary><c>{path}::{name}#{kind}@{line}</c>, printed on every definition head.</summary>
    public static string DefId(string path, string name, string kind, int line) =>
        string.Create(CultureInfo.InvariantCulture, $"{path}::{name}#{kind}@{line}");

    /// <summary>The id, then <c>{path}:{line} {kind}</c>.</summary>
    public static string DefHead(string path, string name, string kind, int line) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{DefId(path, name, kind, line)} {path}:{line} {kind}"
        );

    /// <summary><c>path::name#kind@line</c> → its parts, or <see langword="null"/>.</summary>
    public static (string Path, string Name, string Kind, int Line)? ParseId(string id)
    {
        var at = id.LastIndexOf('@');
        if (
            at < 0
            || !int.TryParse(
                id.AsSpan(at + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var line
            )
        )
        {
            return null;
        }

        var rest = id[..at];
        var hash = rest.LastIndexOf('#');
        if (hash < 0)
        {
            return null;
        }

        var (head, kind) = (rest[..hash], rest[(hash + 1)..]);
        var sep = head.IndexOf("::", StringComparison.Ordinal);
        if (sep < 0)
        {
            return null;
        }

        var (path, name) = (head[..sep], head[(sep + 2)..]);
        return path.Length == 0 || name.Length == 0 || kind.Length == 0
            ? null
            : (path, name, kind, line);
    }

    /// <summary>Every row of <paramref name="defs"/> with its body and callees.</summary>
    public static string Text(
        Ctx cx,
        string root,
        string name,
        IEnumerable<(string Path, string Kind, int Line, int EndLine)> defs
    )
    {
        var budget = (int)Math.Min(Indexer.Config(cx).BodyLines, int.MaxValue);
        var calls = Callees(cx.Host.SymbolCallees(root, name));
        var files = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        var staleSaid = new HashSet<string>(StringComparer.Ordinal);
        var archive = (byte[] uncut) => cx.Host.PutArchiveFor(uncut, cx.AgentId);
        var output = new StringBuilder();
        foreach (var (path, kind, line, endLine) in defs)
        {
            if (!files.TryGetValue(path, out var bytes))
            {
                files[path] = bytes = ReadOrNull(Path.Join(root, path));
            }

            var body = Body(cx, root, name, (path, kind, line, endLine), bytes, budget, archive);
            if (body is null)
            {
                if (staleSaid.Add(path))
                {
                    output.Append("stale ").Append(path).Append('\n');
                }

                body = "";
            }

            output.Append(DefHead(path, name, kind, line)).Append('\n').Append(body);
            if (calls.TryGetValue((path, line), out var names))
            {
                output.Append(CallsLine(names, budget / 2));
            }
        }

        return output.ToString();
    }

    /// <summary>Lines <paramref name="line"/>..=<paramref name="endLine"/> of
    /// <paramref name="src"/>, at most <paramref name="budget"/>. Past it the uncut span is
    /// archived and the answer says <c>… N more lines, expand &lt;id&gt;</c>.</summary>
    public static string Lines(
        string src,
        int line,
        int endLine,
        int budget,
        Func<byte[], string> archive
    )
    {
        var first = Math.Max(line, 1) - 1;
        var last = Math.Max(endLine, line);
        var body = TextLines.Split(src).Skip(first).Take(last - first).ToList();
        var output = new StringBuilder();
        foreach (var l in body.Take(budget))
        {
            output.Append(l).Append('\n');
        }

        if (body.Count > budget)
        {
            var bytes = Encoding.UTF8.GetBytes(src);
            var (start, end) = LineByteSpan(bytes, first, last);
            var id = archive(bytes[start..end]);
            output.Append(
                CultureInfo.InvariantCulture,
                $"  … {body.Count - budget} more lines, expand {ArchiveIds.Abbreviate(id)}\n"
            );
        }

        return output.ToString();
    }

    // The body from the indexed byte span when the file still has the indexed bytes; from the
    // line range when the row has no span; null when the file changed or cannot be read.
    private static string? Body(
        Ctx cx,
        string root,
        string name,
        (string Path, string Kind, int Line, int EndLine) def,
        byte[]? bytes,
        int budget,
        Func<byte[], string> archive
    )
    {
        var span = cx.Host.GetSymbolSpan(root, def.Path, name, def.Kind, def.Line);
        if (span is null || span.ContentHash.Length == 0)
        {
            var src = bytes is null ? "" : Encoding.UTF8.GetString(bytes);
            return Lines(src, def.Line, def.EndLine, budget, archive);
        }

        if (
            bytes is null
            || !string.Equals(Extraction.Sha256(bytes), span.FileSha, StringComparison.Ordinal)
            || span.StartByte < 0
            || span.EndByte > bytes.Length
            || span.StartByte > span.EndByte
        )
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(
            bytes,
            (int)span.StartByte,
            (int)(span.EndByte - span.StartByte)
        );
        var n = TextLines.Split(text).Count;
        return n == 0 ? "" : Lines(text, 1, n, budget, archive);
    }

    private static Dictionary<(string, int), List<string>> Callees(
        IReadOnlyList<(string DefPath, int DefLine, string Callee, int FirstLine)> rows
    )
    {
        var byDef = new Dictionary<(string, int), List<string>>();
        foreach (var (path, line, callee, _) in rows)
        {
            if (!byDef.TryGetValue((path, line), out var names))
            {
                byDef[(path, line)] = names = [];
            }

            names.Add(callee);
        }

        return byDef;
    }

    private static string CallsLine(List<string> names, int cap)
    {
        var shown = Math.Min(names.Count, cap);
        var listed = string.Join(", ", names.Take(shown));
        var extra = names.Count - shown;
        return extra > 0
            ? string.Create(CultureInfo.InvariantCulture, $"calls: {listed} (+{extra})\n")
            : $"calls: {listed}\n";
    }

    // Byte offsets of lines [first, last), 0-based.
    private static (int Start, int End) LineByteSpan(byte[] src, int first, int last)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < src.Length; i++)
        {
            if (src[i] == (byte)'\n')
            {
                starts.Add(i + 1);
            }
        }

        var start = first < starts.Count ? starts[first] : src.Length;
        var end = last < starts.Count ? starts[last] : src.Length;
        return (Math.Min(start, end), end);
    }

    private static byte[]? ReadOrNull(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
