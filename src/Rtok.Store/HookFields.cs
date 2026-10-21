using System.Text.Json.Nodes;

namespace Rtok.Store;

/// <summary>The session fields every hook stdin repeats are saved once per distinct value set in
/// <c>hook_sessions</c>; <c>call_io.request_json</c> keeps only the event's own fields (rtok
/// T433). Readers splice the two objects back together as text.</summary>
internal static class HookFields
{
    /// <summary>The fields a host sends with every hook event of one session or sub-agent, in
    /// the order the saved text lists them, so one value set is always one text.</summary>
    private static readonly string[] SessionKeys =
    [
        "session_id",
        "transcript_path",
        "cwd",
        "scratchpad_dir",
        "permission_mode",
        "effort",
        "agent_id",
        "agent_type",
    ];

    /// <summary><paramref name="stdin"/> cleaned like <see cref="Sanitize.Body"/> and split into
    /// the event's own fields and the session fields, both compact JSON objects. Null when it is
    /// not a JSON object or carries no session field: the caller then saves it whole.</summary>
    public static (byte[] Event, string Fields)? Split(byte[] stdin)
    {
        JsonObject? body;
        try
        {
            body = JsonNode.Parse(stdin) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
        if (body is null)
        {
            return null;
        }
        var fields = new JsonObject();
        foreach (var key in SessionKeys)
        {
            if (body.TryGetPropertyValue(key, out var value))
            {
                body.Remove(key);
                fields[key] = value;
            }
        }
        if (fields.Count == 0)
        {
            return null;
        }
        Sanitize.Strings(body, Sanitize.Text);
        Sanitize.Strings(fields, Sanitize.Text);
        return (
            Sanitize.Serialize(body),
            System.Text.Encoding.UTF8.GetString(Sanitize.Serialize(fields))
        );
    }

    /// <summary>The full stdin from a saved event body and its session fields. Both are compact
    /// objects with disjoint keys, so splicing their members is valid JSON without a parse;
    /// anything else comes back unchanged.</summary>
    public static string Rebuild(string body, string? fields)
    {
        if (Inner(fields) is not { } f || Inner(body) is not { } b)
        {
            return body;
        }
        var separator = f.Length == 0 || b.Length == 0 ? "" : ",";
        return $"{{{f}{separator}{b}}}";
    }

    private static string? Inner(string? s) => s is ['{', .., '}'] ? s[1..^1] : null;
}
