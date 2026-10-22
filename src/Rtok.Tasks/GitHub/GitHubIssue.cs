using System.Text.Json;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.GitHub;

/// <summary>The fields of a GitHub issue the adapter reads.</summary>
internal sealed record GitHubIssue(
    long DatabaseId,
    long Number,
    string? NodeId,
    string Title,
    string Body,
    bool Closed,
    string? StateReason,
    IReadOnlyList<string> Labels,
    string Url,
    long CreatedAt,
    long UpdatedAt,
    bool IsPull
)
{
    public TaskId? Id => RemoteLabels.IdOf(Labels);

    public static GitHubIssue Parse(JsonElement e) =>
        new(
            RemoteJson.Long(e, "id"),
            RemoteJson.Long(e, "number"),
            RemoteJson.OptStr(e, "node_id"),
            RemoteJson.Str(e, "title"),
            RemoteJson.OptStr(e, "body") ?? "",
            string.Equals(RemoteJson.Str(e, "state"), "closed", StringComparison.Ordinal),
            RemoteJson.OptStr(e, "state_reason"),
            RemoteJson.Labels(e, "labels"),
            RemoteJson.Str(e, "html_url"),
            RemoteJson.Unix(e, "created_at"),
            RemoteJson.Unix(e, "updated_at"),
            RemoteJson.Find(e, "pull_request") is not null
        );

    /// <summary>The task the issue holds, or null when no label names one.</summary>
    public PlanTask? ToTask()
    {
        if (Id is not { } id)
        {
            return null;
        }
        var task = new PlanTask
        {
            Id = id,
            Title = RemoteLabels.StripTitle(id, Title),
            Description = Body.Trim(),
            Status = Status,
            Parent = id.Parent,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            External = new ExternalRef("github", Number, Url, NodeId),
        };
        RemoteLabels.Read(Labels, task);
        return task;
    }

    private TaskState Status =>
        Closed
            ? StateReason is "not_planned" or "duplicate"
                ? TaskState.Closed
                : TaskState.Done
            : RemoteLabels.Has(Labels, RemoteLabels.InProgress)
                ? TaskState.InProgress
                : TaskState.Open;
}
