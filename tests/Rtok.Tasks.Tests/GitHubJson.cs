using System.Globalization;
using System.Text.Json;

namespace Rtok.Tasks.Tests;

/// <summary>Issues as GitHub answers them: the database id is the number times 100.</summary>
internal static class GitHubJson
{
    public static string Issue(
        long number,
        string title,
        IEnumerable<string> labels,
        string state = "open",
        string? reason = null,
        bool pull = false,
        string? body = null
    ) =>
        TaskJson.Render(
            w => Write(w, number, title, labels, state, reason, pull, body),
            indented: false
        );

    public static string Issues(params string[] issues) => $"[{string.Join(',', issues)}]";

    private static void Write(
        Utf8JsonWriter w,
        long number,
        string title,
        IEnumerable<string> labels,
        string state,
        string? reason,
        bool pull,
        string? body
    )
    {
        w.WriteStartObject();
        w.WriteNumber("id", number * 100);
        w.WriteNumber("number", number);
        w.WriteString("node_id", $"I_{number.ToString(CultureInfo.InvariantCulture)}");
        w.WriteString("title", title);
        w.WriteString("body", body);
        w.WriteString("state", state);
        w.WriteString("state_reason", reason);
        w.WriteStartArray("labels");
        foreach (var label in labels)
        {
            w.WriteStartObject();
            w.WriteString("name", label);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteString(
            "html_url",
            $"https://github.com/me/app/issues/{number.ToString(CultureInfo.InvariantCulture)}"
        );
        w.WriteString("created_at", "2026-10-07T10:00:00Z");
        w.WriteString("updated_at", "2026-10-07T10:00:05Z");
        if (pull)
        {
            w.WriteStartObject("pull_request");
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }
}
