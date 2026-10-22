using System.Text.Json;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.GitLab;

/// <summary>The fields of a GitLab issue the adapter reads.</summary>
internal sealed record GitLabIssue(
    long Iid,
    long ProjectId,
    string Title,
    string Description,
    bool Closed,
    IReadOnlyList<string> Labels,
    string Url,
    long CreatedAt,
    long UpdatedAt
)
{
    public const string InProgress = "status::in-progress";
    public const string Done = "status::done";
    public const string WontDo = "status::wont-do";

    /// <summary>The status labels, in the order removals list them.</summary>
    public static readonly IReadOnlyList<string> StatusLabels = [InProgress, Done, WontDo];

    public TaskId? Id => RemoteLabels.IdOf(Labels);

    public static GitLabIssue Parse(JsonElement e) =>
        new(
            RemoteJson.Long(e, "iid"),
            RemoteJson.Long(e, "project_id"),
            RemoteJson.Str(e, "title"),
            RemoteJson.OptStr(e, "description") ?? "",
            string.Equals(RemoteJson.Str(e, "state"), "closed", StringComparison.Ordinal),
            RemoteJson.Labels(e, "labels"),
            RemoteJson.Str(e, "web_url"),
            RemoteJson.Unix(e, "created_at"),
            RemoteJson.Unix(e, "updated_at")
        );

    /// <summary>The status label of a state; none for open.</summary>
    public static string? StatusLabel(TaskState state) =>
        state switch
        {
            TaskState.InProgress => InProgress,
            TaskState.Done => Done,
            TaskState.Closed => WontDo,
            _ => null,
        };

    public static bool IsStatus(string label) =>
        StatusLabels.Any(s => string.Equals(s, label, StringComparison.OrdinalIgnoreCase));

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
            Description = Description.Trim(),
            Status = Status,
            Parent = id.Parent,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            External = new ExternalRef("gitlab", Iid, Url, null),
        };
        RemoteLabels.Read(Labels, task);
        return task;
    }

    private TaskState Status =>
        Closed
            ? RemoteLabels.Has(Labels, WontDo)
                ? TaskState.Closed
                : TaskState.Done
            : RemoteLabels.Has(Labels, InProgress)
                ? TaskState.InProgress
                : TaskState.Open;
}
