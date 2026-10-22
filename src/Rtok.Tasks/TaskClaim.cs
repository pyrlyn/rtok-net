namespace Rtok.Tasks;

/// <summary>An agent's claim of a task.</summary>
/// <param name="Id">The task.</param>
/// <param name="Agent">The full agent id.</param>
/// <param name="Title">The task's title when claimed.</param>
/// <param name="Since">When claimed, in Unix seconds.</param>
public sealed record TaskClaim(TaskId Id, string Agent, string Title, long Since);
