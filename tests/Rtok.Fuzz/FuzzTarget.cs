using SharpFuzz;

namespace Rtok.Fuzz;

/// <param name="Name">What <c>rtok-fuzz &lt;name&gt;</c> selects.</param>
/// <param name="Task">The plan task that wires the target.</param>
/// <param name="Body">One input through the code under test; it must not throw.</param>
internal sealed record FuzzTarget(string Name, string Task, ReadOnlySpanAction? Body);
