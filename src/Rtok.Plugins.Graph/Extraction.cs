using System.Security.Cryptography;
using System.Text;
using Pyrlyn.TreeSitter;
using Rtok.Plugin.Sdk;
using Rtok.Plugins.Read;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The rows the index stores for one file: tag hits from <see cref="Outline.Tags(string, ReadOnlyMemory{byte}, IReadOnlyDictionary{string, string}?)"/>,
/// each reference scoped to the innermost definition around it, each definition with its
/// signature, doc comment and content hash. The graph diff reads a git blob through the same
/// code, so both sides of a diff are extracted alike.
/// </summary>
internal static class Extraction
{
    /// <summary>Bump when <see cref="Scoped"/> changes: every root re-indexes once.</summary>
    public const uint IndexVersion = 5;

    /// <summary>Doc comment bytes kept on a definition.</summary>
    public const int DocCap = 512;

    /// <summary>The rows of <paramref name="utf8"/>; <see langword="null"/> when the grammar's
    /// query fails.</summary>
    public static List<SymbolRow>? RowsOf(
        string path,
        ReadOnlyMemory<byte> utf8,
        IReadOnlyDictionary<string, string> extensions
    )
    {
        IReadOnlyList<TagHit> hits;
        try
        {
            hits = Outline.Tags(path, utf8, extensions);
        }
        catch (QueryException)
        {
            return null;
        }

        return Scoped(utf8.Span, hits);
    }

    /// <summary>
    /// Hex sha256 of <see cref="IndexVersion"/>, every grammar's tags and locals query and ABI
    /// version, and the configured suffix → grammar map, sorted so map order keeps the hash.
    /// A change to any of them leaves an untouched file's rows from the old extractor, which
    /// the stat and sha gates cannot see; a mismatch indexes the root cold.
    /// </summary>
    public static string Fingerprint(IReadOnlyDictionary<string, string> extensions)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> word = stackalloc byte[4];
        BitConverter.TryWriteBytes(word, IndexVersion);
        hash.AppendData(word);
        foreach (var grammar in Grammar.All)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(grammar.Id));
            BitConverter.TryWriteBytes(word, grammar.Language.AbiVersion);
            hash.AppendData(word);
            hash.AppendData(Encoding.UTF8.GetBytes(grammar.TagsQuery));
            hash.AppendData(Encoding.UTF8.GetBytes(grammar.LocalsQuery));
        }

        foreach (var (ext, name) in extensions.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes($"\0ext:{ext}={name}"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Hex sha256 of <paramref name="bytes"/>.</summary>
    public static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// One row per hit. An import keeps its full specifier as the scope; a definition has
    /// none; a reference gets the innermost definition whose lines enclose it, ties to the
    /// smaller span so a nested <c>fn</c> wins over the <c>impl</c> around it, and
    /// <c>""</c> (file level) outside every definition.
    /// </summary>
    internal static List<SymbolRow> Scoped(ReadOnlySpan<byte> src, IReadOnlyList<TagHit> hits)
    {
        var defs = hits.Where(h => h.IsDef)
            .Select(h => (Start: h.Line, End: Math.Max(h.EndLine, h.Line), h.Name))
            .ToList();
        var text = Encoding.UTF8.GetString(src);
        List<string>? lines = null;
        var rows = new List<SymbolRow>(hits.Count);
        foreach (var h in hits)
        {
            string scope;
            if (h.Kind is "import")
            {
                scope = h.ImportPath;
            }
            else if (h.IsDef)
            {
                scope = "";
            }
            else
            {
                scope = Enclosing(defs, h.Line);
            }

            var doc = "";
            if (h.IsDef)
            {
                lines ??= [.. TextLines.Split(text)];
                doc = DocAbove(lines, h.Line);
            }

            rows.Add(
                new SymbolRow(h.Name, h.Kind, h.Line, h.IsDef, h.EndLine, scope)
                {
                    StartByte = h.StartByte,
                    EndByte = h.EndByte,
                    ContentHash = ContentHash(src, h.StartByte, h.EndByte),
                    Signature = h.IsDef ? h.LineText.TrimEnd('\r', '\n') : "",
                    Doc = doc,
                }
            );
        }

        return rows;
    }

    /// <summary>Contiguous <c>///</c> or <c>//!</c> lines directly above 1-based
    /// <paramref name="line"/>, at most <see cref="DocCap"/> UTF-8 bytes; a blank line ends
    /// the comment.</summary>
    internal static string DocAbove(IReadOnlyList<string> lines, int line)
    {
        if (line <= 1)
        {
            return "";
        }

        var block = new List<string>();
        for (var i = Math.Min(line - 1, lines.Count) - 1; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();
            if (
                !trimmed.StartsWith("///", StringComparison.Ordinal)
                && !trimmed.StartsWith("//!", StringComparison.Ordinal)
            )
            {
                break;
            }

            block.Add(lines[i]);
        }

        block.Reverse();
        return Clip(block);
    }

    // The lines joined by newlines, cut at a rune boundary to DocCap UTF-8 bytes.
    private static string Clip(List<string> block)
    {
        var doc = new StringBuilder();
        var bytes = 0;
        for (var n = 0; n < block.Count; n++)
        {
            if (n > 0)
            {
                if (bytes + 1 >= DocCap)
                {
                    break;
                }

                doc.Append('\n');
                bytes++;
            }

            var room = DocCap - bytes;
            var size = Encoding.UTF8.GetByteCount(block[n]);
            if (size <= room)
            {
                doc.Append(block[n]);
                bytes += size;
                continue;
            }

            foreach (var rune in block[n].EnumerateRunes())
            {
                if (rune.Utf8SequenceLength > room)
                {
                    break;
                }

                doc.Append(rune.ToString());
                room -= rune.Utf8SequenceLength;
            }

            break;
        }

        return doc.ToString();
    }

    private static string Enclosing(List<(int Start, int End, string Name)> defs, int line)
    {
        var best = "";
        var span = int.MaxValue;
        foreach (var (start, end, name) in defs)
        {
            if (start <= line && line <= end && end - start < span)
            {
                (best, span) = (name, end - start);
            }
        }

        return best;
    }

    private static string ContentHash(ReadOnlySpan<byte> src, int start, int end)
    {
        var from = Math.Clamp(start, 0, src.Length);
        var to = Math.Clamp(end, from, src.Length);
        return Sha256(src[from..to]);
    }
}
