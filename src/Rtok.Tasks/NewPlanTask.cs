namespace Rtok.Tasks;

/// <summary>What <c>task create</c> asks for; the id comes from the allocator.</summary>
/// <param name="Title">The title.</param>
/// <param name="Description">The body.</param>
/// <param name="Parent">The parent, for a subtask.</param>
public sealed record NewPlanTask(string Title, string Description = "", TaskId? Parent = null);
