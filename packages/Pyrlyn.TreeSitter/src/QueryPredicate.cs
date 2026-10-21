namespace Pyrlyn.TreeSitter;

/// <summary>A predicate the query engine leaves to the caller, such as <c>#strip!</c>.</summary>
/// <param name="Operator">The predicate name with its <c>?</c> or <c>!</c>.</param>
/// <param name="Args">The arguments in order.</param>
public sealed record QueryPredicate(string Operator, IReadOnlyList<QueryPredicateArg> Args);
