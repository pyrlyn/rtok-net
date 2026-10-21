using System.Text;

namespace Pyrlyn.TreeSitter;

// The Rust binding's error shape: the offending name for node type, field and capture
// errors; the offending line with a caret under the error for the others.
internal static class QueryErrors
{
    private const uint NodeType = 2;
    private const uint Field = 3;
    private const uint Capture = 4;
    private const uint Structure = 5;
    private const uint LanguageError = 6;

    public static QueryException Compile(Language language, byte[] source, int offset, uint type)
    {
        if (type == LanguageError)
        {
            return new QueryException(
                QueryErrorKind.Language,
                0,
                0,
                0,
                $"Incompatible language version {language.AbiVersion}."
            );
        }
        offset = Math.Clamp(offset, 0, source.Length);
        var row = RowOf(source, offset);
        var lineStart = offset;
        while (lineStart > 0 && source[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }
        var column = offset - lineStart;
        if (type is NodeType or Field or Capture)
        {
            var kind = type switch
            {
                NodeType => QueryErrorKind.NodeType,
                Field => QueryErrorKind.Field,
                _ => QueryErrorKind.Capture,
            };
            return new QueryException(kind, row, column, offset, Name(source, offset));
        }
        return new QueryException(
            type == Structure ? QueryErrorKind.Structure : QueryErrorKind.Syntax,
            row,
            column,
            offset,
            Line(source, lineStart, column)
        );
    }

    public static QueryException Predicate(int row, string message) =>
        new(QueryErrorKind.Predicate, row, 0, 0, message);

    public static int RowOf(byte[] source, int offset) =>
        source.AsSpan(0, Math.Min(offset, source.Length)).Count((byte)'\n');

    // The name at offset: up to the closing quote when it is quoted, else a run of
    // identifier characters.
    private static string Name(byte[] source, int offset)
    {
        var suffix = Encoding.UTF8.GetString(source, offset, source.Length - offset);
        var inQuotes = offset > 0 && source[offset - 1] == (byte)'"';
        var end = 0;
        var backslashes = 0;
        while (end < suffix.Length)
        {
            var c = suffix[end];
            if (inQuotes)
            {
                if (c == '"' && backslashes % 2 == 0)
                {
                    break;
                }
                backslashes = c == '\\' ? backslashes + 1 : 0;
            }
            else if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
            {
                break;
            }
            end++;
        }
        return suffix[..end];
    }

    private static string Line(byte[] source, int lineStart, int column)
    {
        if (lineStart >= source.Length)
        {
            return "Unexpected EOF";
        }
        var length = source.AsSpan(lineStart).IndexOf((byte)'\n');
        var line = Encoding.UTF8.GetString(
            source,
            lineStart,
            length < 0 ? source.Length - lineStart : length
        );
        return line + "\n" + new string(' ', column) + "^";
    }
}
