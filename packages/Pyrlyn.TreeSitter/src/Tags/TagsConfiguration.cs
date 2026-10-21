using System.Text;
using System.Text.RegularExpressions;

namespace Pyrlyn.TreeSitter.Tags;

/// <summary>
/// A tags query for one grammar: the locals query and the tags query compiled together, as
/// tree-sitter-tags 0.25 does. Immutable and safe to share across threads.
/// </summary>
/// <remarks>
/// Captures: <c>@name</c>, <c>@doc</c>, <c>@ignore</c>, <c>@definition.&lt;kind&gt;</c>,
/// <c>@reference.&lt;kind&gt;</c> and the locals ones, <c>@local.scope</c>,
/// <c>@local.definition</c>, <c>@local.reference</c>. Per pattern: <c>(#is-not? local)</c>
/// drops a name defined in an enclosing local scope, <c>(#set! local.scope-inherits false)</c>
/// stops that lookup at the scope, <c>(#select-adjacent! @doc @x)</c> keeps only the doc nodes
/// that end right above <c>@x</c>, and <c>(#strip! @doc "regex")</c> removes text from them.
/// </remarks>
public sealed class TagsConfiguration
{
    private readonly List<string> _syntaxTypeNames = [];

    /// <summary>Compiles <paramref name="localsQuery"/> followed by <paramref name="tagsQuery"/>.</summary>
    /// <exception cref="QueryException">
    /// The query does not compile, uses a capture outside the list above, or has a bad
    /// <c>#strip!</c> regex.
    /// </exception>
    public TagsConfiguration(Language language, string tagsQuery, string localsQuery)
    {
        ArgumentNullException.ThrowIfNull(tagsQuery);
        ArgumentNullException.ThrowIfNull(localsQuery);
        Language = language;
        Query = new Query(language, localsQuery + tagsQuery);
        try
        {
            var tagsOffset = Encoding.UTF8.GetByteCount(localsQuery);
            for (var i = 0; i < Query.PatternCount; i++)
            {
                if (Query.StartByteForPattern(i) < tagsOffset)
                {
                    TagsPatternIndex++;
                }
            }
            CaptureSyntaxType = new int[Query.CaptureNames.Count];
            CaptureIsDefinition = new bool[Query.CaptureNames.Count];
            for (var i = 0; i < Query.CaptureNames.Count; i++)
            {
                ReadCapture(i, Query.CaptureNames[i]);
            }
            Patterns = new PatternInfo[Query.PatternCount];
            for (var pattern = 0; pattern < Query.PatternCount; pattern++)
            {
                Patterns[pattern] = ReadPattern(pattern);
            }
        }
        catch
        {
            Query.Dispose();
            throw;
        }
    }

    /// <summary>The grammar.</summary>
    public Language Language { get; }

    /// <summary>The compiled locals and tags patterns.</summary>
    public Query Query { get; }

    /// <summary>The tag kinds, by <see cref="Tag.SyntaxTypeId"/>.</summary>
    public IReadOnlyList<string> SyntaxTypeNames => _syntaxTypeNames;

    internal int TagsPatternIndex { get; }

    internal int? NameCapture { get; private set; }

    internal int? IgnoreCapture { get; private set; }

    internal int? DocCapture { get; private set; }

    internal int? LocalScopeCapture { get; private set; }

    internal int? LocalDefinitionCapture { get; private set; }

    // Per capture index: the syntax type id (-1 when the capture names no tag) and whether
    // it is a definition.
    internal int[] CaptureSyntaxType { get; }

    internal bool[] CaptureIsDefinition { get; }

    internal PatternInfo[] Patterns { get; }

    /// <summary>The kind of a tag, such as <c>function</c> or <c>call</c>.</summary>
    public string SyntaxTypeName(int syntaxTypeId) => _syntaxTypeNames[syntaxTypeId];

    private void ReadCapture(int index, string name)
    {
        CaptureSyntaxType[index] = -1;
        switch (name)
        {
            case "name":
                NameCapture = index;
                return;
            case "ignore":
                IgnoreCapture = index;
                return;
            case "doc":
                DocCapture = index;
                return;
            case "local.scope":
                LocalScopeCapture = index;
                return;
            case "local.definition":
                LocalDefinitionCapture = index;
                return;
            case "local.reference" or "":
                return;
        }
        string kind;
        if (name.StartsWith("definition.", StringComparison.Ordinal))
        {
            kind = name["definition.".Length..];
            CaptureIsDefinition[index] = true;
        }
        else if (name.StartsWith("reference.", StringComparison.Ordinal))
        {
            kind = name["reference.".Length..];
        }
        else
        {
            throw new QueryException(
                QueryErrorKind.Capture,
                0,
                0,
                0,
                $"Invalid capture @{name}. Expected one of: @definition.*, @reference.*, @doc, @name, @local.(scope|definition|reference)."
            );
        }
        var id = _syntaxTypeNames.IndexOf(kind);
        if (id < 0)
        {
            id = _syntaxTypeNames.Count;
            _syntaxTypeNames.Add(kind);
        }
        CaptureSyntaxType[index] = id;
    }

    private PatternInfo ReadPattern(int pattern)
    {
        var info = new PatternInfo { LocalScopeInherits = true };
        foreach (var (property, isPositive) in Query.PropertyPredicates(pattern))
        {
            if (!isPositive && string.Equals(property.Key, "local", StringComparison.Ordinal))
            {
                info.NameMustBeNonLocal = true;
            }
        }
        foreach (var property in Query.PropertySettings(pattern))
        {
            if (
                string.Equals(property.Key, "local.scope-inherits", StringComparison.Ordinal)
                && string.Equals(property.Value, "false", StringComparison.Ordinal)
            )
            {
                info.LocalScopeInherits = false;
            }
        }
        if (DocCapture is not { } doc)
        {
            return info;
        }
        foreach (var predicate in Query.GeneralPredicates(pattern))
        {
            if (predicate.Args.Count < 2 || predicate.Args[0].CaptureId != doc)
            {
                continue;
            }
            var arg = predicate.Args[1];
            if (IsOperator(predicate, "select-adjacent!") && arg.CaptureId is { } adjacent)
            {
                info.DocsAdjacentCapture = adjacent;
            }
            else if (IsOperator(predicate, "strip!") && arg.Text is { } strip)
            {
                info.DocStrip =
                    TextPredicate.CompileRegex(strip)
                    ?? throw new QueryException(
                        QueryErrorKind.Predicate,
                        0,
                        0,
                        0,
                        $"Invalid regex '{strip}'"
                    );
            }
        }
        return info;
    }

    private static bool IsOperator(QueryPredicate predicate, string name) =>
        string.Equals(predicate.Operator, name, StringComparison.Ordinal);

    internal sealed class PatternInfo
    {
        public int? DocsAdjacentCapture { get; set; }

        public bool LocalScopeInherits { get; set; }

        public bool NameMustBeNonLocal { get; set; }

        public Regex? DocStrip { get; set; }
    }
}
