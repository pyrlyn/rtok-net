namespace Pyrlyn.TreeSitter;

/// <summary>Why a query did not compile.</summary>
public enum QueryErrorKind
{
    /// <summary>Malformed query text.</summary>
    Syntax,

    /// <summary>A node type the grammar does not have.</summary>
    NodeType,

    /// <summary>A field the grammar does not have.</summary>
    Field,

    /// <summary>A capture used in a predicate but not in the pattern.</summary>
    Capture,

    /// <summary>A pattern that no tree can match.</summary>
    Structure,

    /// <summary>A predicate with wrong arguments.</summary>
    Predicate,

    /// <summary>The grammar's ABI is not supported.</summary>
    Language,
}
