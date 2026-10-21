using System.Text;
using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

/// <summary>
/// A compiled tree-sitter query. Text predicates (<c>#eq?</c>, <c>#match?</c>, <c>#any-of?</c> and
/// their <c>not-</c>/<c>any-</c> forms) are applied by <see cref="QueryCursor"/>; property and
/// general predicates are exposed per pattern. Immutable and safe to share across threads.
/// </summary>
public sealed class Query : IDisposable
{
    private readonly QueryHandle _handle;
    private readonly string[] _captureNames;
    private readonly PredicateParser[] _patterns;
    private readonly int[] _patternStartBytes;

    /// <summary>Compiles <paramref name="source"/> for <paramref name="language"/>.</summary>
    /// <exception cref="QueryException">The query does not compile.</exception>
    public unsafe Query(Language language, string source)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);
        Language = language;
        var bytes = Encoding.UTF8.GetBytes(source);
        uint errorOffset = 0;
        uint errorType = 0;
        nint query;
        fixed (byte* p = bytes)
        {
            query = NativeMethods.QueryNew(
                language.Handle,
                p,
                (uint)bytes.Length,
                &errorOffset,
                &errorType
            );
        }
        if (query == 0)
        {
            throw QueryErrors.Compile(language, bytes, (int)errorOffset, errorType);
        }
        _handle = new QueryHandle(query);
        try
        {
            _captureNames = ReadStrings(
                query,
                NativeMethods.QueryCaptureCount(query),
                &NativeMethods.QueryCaptureNameForId
            );
            var strings = ReadStrings(
                query,
                NativeMethods.QueryStringCount(query),
                &NativeMethods.QueryStringValueForId
            );
            PatternCount = (int)NativeMethods.QueryPatternCount(query);
            _patternStartBytes = new int[PatternCount];
            _patterns = ReadPatterns(query, bytes, strings);
        }
        catch
        {
            _handle.Dispose();
            throw;
        }
    }

    /// <summary>The grammar the query was compiled for.</summary>
    public Language Language { get; }

    /// <summary>Number of patterns.</summary>
    public int PatternCount { get; }

    /// <summary>Capture names by capture index, without the <c>@</c>.</summary>
    public IReadOnlyList<string> CaptureNames => _captureNames;

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>The index of capture <paramref name="name"/>, or null when the query has none.</summary>
    public int? CaptureIndex(string name)
    {
        var index = Array.IndexOf(_captureNames, name);
        return index < 0 ? null : index;
    }

    /// <summary>Byte offset of the pattern in the query text.</summary>
    public int StartByteForPattern(int pattern) => _patternStartBytes[pattern];

    /// <summary>The <c>#set!</c> properties of a pattern.</summary>
    public IReadOnlyList<QueryProperty> PropertySettings(int pattern) =>
        _patterns[pattern].Settings;

    /// <summary>The <c>#is?</c> (positive) and <c>#is-not?</c> properties of a pattern.</summary>
    public IReadOnlyList<(QueryProperty Property, bool IsPositive)> PropertyPredicates(
        int pattern
    ) => _patterns[pattern].Properties;

    /// <summary>The predicates of a pattern that the engine does not interpret.</summary>
    public IReadOnlyList<QueryPredicate> GeneralPredicates(int pattern) =>
        _patterns[pattern].General;

    /// <inheritdoc />
    public void Dispose() => _handle.Dispose();

    internal bool SatisfiesTextPredicates(
        int pattern,
        ReadOnlySpan<QueryCapture> captures,
        ReadOnlySpan<byte> source
    )
    {
        foreach (var predicate in _patterns[pattern].Text)
        {
            if (!predicate.IsSatisfied(captures, source))
            {
                return false;
            }
        }
        return true;
    }

    private unsafe PredicateParser[] ReadPatterns(nint query, byte[] bytes, string[] strings)
    {
        var patterns = new PredicateParser[PatternCount];
        for (var pattern = 0; pattern < PatternCount; pattern++)
        {
            var start = (int)NativeMethods.QueryStartByteForPattern(query, (uint)pattern);
            _patternStartBytes[pattern] = start;
            uint stepCount = 0;
            var steps = NativeMethods.QueryPredicatesForPattern(query, (uint)pattern, &stepCount);
            patterns[pattern] = new PredicateParser(
                _captureNames,
                strings,
                QueryErrors.RowOf(bytes, start),
                new ReadOnlySpan<TSQueryPredicateStep>(steps, (int)stepCount)
            );
        }
        return patterns;
    }

    private static unsafe string[] ReadStrings(
        nint query,
        uint count,
        delegate* <nint, uint, uint*, byte*> read
    )
    {
        var values = new string[count];
        for (var i = 0u; i < count; i++)
        {
            uint length = 0;
            var value = read(query, i, &length);
            values[i] = Encoding.UTF8.GetString(value, (int)length);
        }
        return values;
    }
}
