using Rtok.Agent.Sdk;

namespace Rtok.Agents.Hosts.Proxy;

/// <summary>
/// aider's <c>.aider.conf.yml</c>: the <c>openai-api-base</c> line, edited as text so comments,
/// quotes elsewhere and line breaks stay (rtok <c>aider::insert_ours</c>). Install rewrites every
/// such line that is not rtok's URL and appends one when there is none; remove drops only the
/// lines that hold rtok's.
/// </summary>
internal sealed class AiderProxy(string path, string url, int port) : OwnedFile(path)
{
    private const string Key = "openai-api-base";

    public override bool Holds(string text) =>
        Lines(text).Exists(l => Split(l.Body) is { } k && ProxyOwner.Ours(k.Value, url, port));

    public override (string Report, string Text) Install(string text)
    {
        var lines = Lines(text);
        string? old = null;
        var found = false;
        var edited = new List<string>();
        foreach (var (body, end) in lines)
        {
            if (Split(body) is not { } k)
            {
                edited.Add(body + end);
                continue;
            }
            found = true;
            if (string.Equals(k.Value, url, StringComparison.Ordinal))
            {
                edited.Add(body + end);
                continue;
            }
            old = k.Value;
            edited.Add($"{k.Indent}{Key}: {url}{(k.Comment is null ? "" : " " + k.Comment)}{end}");
        }
        if (old is not null)
        {
            return ($"+ {Key}: {url}\nrevert: set {Key} to {old}", string.Concat(edited));
        }
        if (found)
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lead = text.Length > 0 && !text.EndsWith('\n') ? nl : "";
        return ($"+ {Key}: {url}\nrevert: remove {Key}", $"{text}{lead}{Key}: {url}{nl}");
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var lines = Lines(text);
        var kept = lines
            .Where(l => Split(l.Body) is not { } k || !ProxyOwner.Ours(k.Value, url, port))
            .ToList();
        return kept.Count == lines.Count
            ? (Agent.Sdk.Apply.NoChanges, text)
            : ($"- {Key}", string.Concat(kept.Select(l => l.Body + l.End)));
    }

    /// <summary>The lines of <paramref name="text"/>, each split from its line break.</summary>
    private static List<(string Body, string End)> Lines(string text)
    {
        var lines = new List<(string, string)>();
        var at = 0;
        while (at < text.Length)
        {
            var nl = text.IndexOf('\n', at);
            var next = nl < 0 ? text.Length : nl + 1;
            var line = text[at..next];
            var body = line.TrimEnd('\n').TrimEnd('\r');
            lines.Add((body, line[body.Length..]));
            at = next;
        }
        return lines;
    }

    /// <summary>
    /// An uncommented <c>openai-api-base:</c> line: its indent, its value unquoted and its
    /// trailing comment; null for anything else, a lookalike key included.
    /// </summary>
    private static (string Indent, string Value, string? Comment)? Split(string line)
    {
        var rest = line.TrimStart(' ', '\t');
        if (!rest.StartsWith(Key, StringComparison.Ordinal))
        {
            return null;
        }
        var after = rest[Key.Length..].TrimStart(' ', '\t');
        if (!after.StartsWith(':'))
        {
            return null;
        }
        after = after[1..];
        var hash = -1;
        for (var i = 0; i < after.Length; i++)
        {
            if (after[i] == '#' && (i == 0 || after[i - 1] is ' ' or '\t'))
            {
                hash = i;
                break;
            }
        }
        var value = (hash < 0 ? after : after[..hash]).Trim();
        if (value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0])
        {
            value = value[1..^1];
        }
        var comment = hash < 0 ? null : after[hash..].TrimEnd();
        return (line[..(line.Length - rest.Length)], value, comment);
    }
}
