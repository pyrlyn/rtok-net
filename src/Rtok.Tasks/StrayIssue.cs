namespace Rtok.Tasks;

/// <summary>An issue labelled <c>rtok</c> without a <c>rtok:&lt;id&gt;</c> label.</summary>
/// <param name="Title">The issue title.</param>
/// <param name="Url">The issue page.</param>
/// <param name="Id">The id the title still names, if any.</param>
public sealed record StrayIssue(string Title, string Url, TaskId? Id);
