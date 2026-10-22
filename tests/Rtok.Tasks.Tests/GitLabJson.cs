using System.Globalization;

namespace Rtok.Tasks.Tests;

/// <summary>Issues as GitLab answers them, in project 42 of <c>me/app</c>.</summary>
internal static class GitLabJson
{
    public static string Issue(
        long iid,
        string title,
        IEnumerable<string> labels,
        string state = "opened"
    ) =>
        TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                w.WriteNumber("id", iid * 100);
                w.WriteNumber("iid", iid);
                w.WriteNumber("project_id", 42);
                w.WriteString("title", title);
                w.WriteNull("description");
                w.WriteString("state", state);
                w.WriteStartArray("labels");
                foreach (var label in labels)
                {
                    w.WriteStringValue(label);
                }
                w.WriteEndArray();
                w.WriteString(
                    "web_url",
                    $"https://gitlab.com/me/app/-/issues/{iid.ToString(CultureInfo.InvariantCulture)}"
                );
                w.WriteString("created_at", "2026-10-07T10:00:00.000Z");
                w.WriteString("updated_at", "2026-10-07T12:00:00.000+02:00");
                w.WriteEndObject();
            },
            indented: false
        );
}
