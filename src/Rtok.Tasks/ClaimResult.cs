namespace Rtok.Tasks;

/// <summary>A claimed task.</summary>
/// <param name="Task">The task as stored.</param>
/// <param name="Changed">False when the agent already held it and nothing was written.</param>
public sealed record ClaimResult(PlanTask Task, bool Changed);
