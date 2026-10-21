namespace Rtok.Fuzz;

/// <summary>An invariant of the code under test does not hold for a fuzz input.</summary>
internal sealed class FuzzInvariantException(string message) : Exception(message);
