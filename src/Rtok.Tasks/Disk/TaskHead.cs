using System.Text.Json.Serialization;

namespace Rtok.Tasks.Disk;

/// <summary>A task file's front matter as read; <see cref="TaskFile"/> writes it.</summary>
internal sealed class TaskHead
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(0)]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    [JsonPropertyOrder(1)]
    public string Title { get; set; } = "";

    [JsonPropertyName("status")]
    [JsonPropertyOrder(2)]
    public string Status { get; set; } = "";

    [JsonPropertyName("parent")]
    [JsonPropertyOrder(3)]
    public string? Parent { get; set; }

    [JsonPropertyName("created_at")]
    [JsonPropertyOrder(4)]
    public long CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonPropertyOrder(5)]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("priority")]
    [JsonPropertyOrder(6)]
    public int? Priority { get; set; }

    [JsonPropertyName("assignee")]
    [JsonPropertyOrder(7)]
    public string? Assignee { get; set; }

    [JsonPropertyName("blocked_by")]
    [JsonPropertyOrder(8)]
    public List<string>? BlockedBy { get; set; }

    [JsonPropertyName("conflicts_with")]
    [JsonPropertyOrder(9)]
    public List<string>? ConflictsWith { get; set; }

    [JsonPropertyName("parallel")]
    [JsonPropertyOrder(10)]
    public bool? Parallel { get; set; }
}
