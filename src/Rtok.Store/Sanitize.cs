using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Core;

namespace Rtok.Store;

/// <summary>Noise a saved request body carries without saying anything (rtok T431): terminal
/// escapes, control and zero-width characters, harness wrapper blocks, trailing whitespace.
/// Pure and idempotent; <see cref="Body"/> is what the store applies unless <c>store_raw</c>.</summary>
public static class Sanitize
{
    /// <summary>Blocks a host wraps around a prompt or a tool result for the model, not for the record.</summary>
    private static readonly (string Open, string Close)[] Tags =
    [
        .. (
            (string[])
                ["system-reminder", "task-notification", "ci-monitor-event", "local-command-caveat"]
        ).Select(t => ($"<{t}>", $"</{t}>")),
    ];

    internal static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A request body cleaned for saving. JSON has every string value cleaned (keys,
    /// numbers and structure kept); other UTF-8 is cleaned as text; anything else is kept. A body
    /// with nothing to clean comes back as the same array, byte for byte.</summary>
    public static byte[] Body(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        switch (TryParse(bytes))
        {
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                var raw = value.GetValue<string>();
                var cleaned = Text(raw);
                return ReferenceEquals(cleaned, raw) ? bytes : Serialize(JsonValue.Create(cleaned));
            case { } node:
                return Strings(node, Text) ? Serialize(node) : bytes;
        }
        string s;
        try
        {
            s = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return bytes;
        }
        var clean = Text(s);
        return ReferenceEquals(clean, s) ? bytes : Encoding.UTF8.GetBytes(clean);
    }

    /// <summary>Runs <paramref name="clean"/> over every string under <paramref name="node"/> in
    /// place; true when any changed. A cleaner returns its input instance when nothing changed.
    /// A bare string has no parent to hold its new value: clean it directly.</summary>
    public static bool Strings(JsonNode? node, Func<string, string> clean)
    {
        ArgumentNullException.ThrowIfNull(clean);
        switch (node)
        {
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                var s = value.GetValue<string>();
                var cleaned = clean(s);
                if (ReferenceEquals(cleaned, s))
                {
                    return false;
                }
                switch (value.Parent)
                {
                    case JsonObject parent:
                        parent[value.GetPropertyName()] = cleaned;
                        return true;
                    case JsonArray parent:
                        parent[value.GetElementIndex()] = cleaned;
                        return true;
                    default:
                        throw new ArgumentException(
                            "a bare string has no parent to hold its cleaned value",
                            nameof(node)
                        );
                }
            case JsonArray array:
                var hitArray = false;
                foreach (var item in array.ToArray())
                {
                    hitArray |= Strings(item, clean);
                }
                return hitArray;
            case JsonObject obj:
                var hitObject = false;
                foreach (var (_, item) in obj.ToArray())
                {
                    hitObject |= Strings(item, clean);
                }
                return hitObject;
            default:
                return false;
        }
    }

    /// <summary><paramref name="s"/> without wrapper blocks, escapes, control and zero-width
    /// characters, with LF line ends, no trailing spaces or tabs and at most one blank line in a
    /// row. Returns the same instance when nothing changed.</summary>
    public static string Text(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        // Characters first: noise inside a tag (`<system-ESC[0mreminder>`) must be gone before
        // the tag is looked for, or the first call leaves a wrapper the second one removes.
        var clean = Whitespace(Unwrap(Characters(s, lineEnds: true)));
        return string.Equals(clean, s, StringComparison.Ordinal) ? s : clean;
    }

    /// <summary><paramref name="s"/> without escapes, control and zero-width characters and
    /// nothing else (rtok T432): wrappers, CR, trailing blanks and blank runs stay, because the
    /// model's exact-match edits fail when a result's whitespace no longer matches the file.
    /// Returns the same instance when nothing changed.</summary>
    public static string TerminalNoise(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var clean = Characters(s, lineEnds: false);
        return string.Equals(clean, s, StringComparison.Ordinal) ? s : clean;
    }

    private static JsonNode? TryParse(byte[] bytes)
    {
        try
        {
            return JsonNode.Parse(bytes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static byte[] Serialize(JsonNode node) =>
        Encoding.UTF8.GetBytes(node.ToJsonString(Compact));

    /// <summary>Drops ANSI escapes, zero-width spaces and BOMs, and control characters other
    /// than LF and tab. With <paramref name="lineEnds"/>, CRLF and a lone CR become LF.</summary>
    internal static string Characters(string s, bool lineEnds)
    {
        var output = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            switch (c)
            {
                case '\x1b':
                    i = Ansi.SkipEscape(s, i);
                    continue;
                case '\r' when lineEnds && i + 1 < s.Length && s[i + 1] == '\n':
                    break;
                case '\r' when lineEnds:
                    output.Append('\n');
                    break;
                case '\n' or '\t' or '\r':
                    output.Append(c);
                    break;
                // U+200D stays: it joins emoji sequences, so dropping it changes what is shown.
                case '​' or '⁠' or '﻿':
                    break;
                default:
                    if (!char.IsControl(c))
                    {
                        output.Append(c);
                    }
                    break;
            }
            i++;
        }
        return output.ToString();
    }

    /// <summary><paramref name="s"/> without wrapper blocks and the newlines after each, to a
    /// fixed point: removing a block can join its neighbours into a new tag.</summary>
    private static string Unwrap(string s)
    {
        while (Strip(s) is { } next)
        {
            s = next;
        }
        return s;
    }

    /// <summary>One left-to-right pass: the leftmost <c>&lt;t&gt;…&lt;/t&gt;</c> at each step,
    /// plus the newlines after it, is dropped and the scan resumes after it. Null when nothing
    /// was removed.</summary>
    private static string? Strip(string s)
    {
        if (!s.Contains("</", StringComparison.Ordinal))
        {
            return null;
        }
        // Where each closer last starts: an opener after it can never match, which keeps a
        // flood of unclosed openers linear.
        var last = Tags.Select(t => s.LastIndexOf(t.Close, StringComparison.Ordinal)).ToArray();
        var output = new StringBuilder();
        var (kept, at) = (0, 0);
        int lt;
        while ((lt = s.IndexOf('<', at)) >= 0)
        {
            at = lt + 1;
            var tag = Array.FindIndex(
                Tags,
                t => string.CompareOrdinal(s, lt, t.Open, 0, t.Open.Length) == 0
            );
            if (tag < 0)
            {
                continue;
            }
            var (open, close) = Tags[tag];
            var body = lt + open.Length;
            if (last[tag] < body)
            {
                continue;
            }
            var q = s.IndexOf(close, body, StringComparison.Ordinal);
            if (q < 0)
            {
                continue;
            }
            output.Append(s, kept, lt - kept);
            at = q + close.Length;
            while (at < s.Length && s[at] == '\n')
            {
                at++;
            }
            kept = at;
        }
        if (kept == 0)
        {
            return null;
        }
        output.Append(s, kept, s.Length - kept);
        return output.ToString();
    }

    /// <summary>Trailing spaces and tabs cut from every line; three or more newlines become two.</summary>
    internal static string Whitespace(string s)
    {
        var output = new StringBuilder(s.Length);
        var newlines = 0;
        var n = 0;
        foreach (var range in s.AsSpan().Split('\n'))
        {
            if (n++ > 0)
            {
                newlines++;
                if (newlines <= 2)
                {
                    output.Append('\n');
                }
            }
            var line = s.AsSpan(range).TrimEnd(" \t");
            if (!line.IsEmpty)
            {
                newlines = 0;
                output.Append(line);
            }
        }
        return output.ToString();
    }
}
