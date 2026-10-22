namespace Rtok.Measure;

/// <summary>One <c>[[tasks]]</c> entry of the bench task file.</summary>
/// <param name="Id">The name in the output.</param>
/// <param name="Prompt">What the agent is asked.</param>
/// <param name="Check">A shell command that exits 0 when the task was done.</param>
public sealed record BenchTask(string Id, string Prompt, string Check);
