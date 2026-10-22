using System.Text;
using System.Text.Unicode;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// Prints a command's captured output: a pointer when this window already saw it, the
/// filtered text with an <c>expand</c> trailer when the cut dropped something, or the raw
/// bytes whenever filtering would not pay. Every outcome records a Measurement.
/// </summary>
internal sealed class Emitter(CmdSettings settings, bool trailer, uint trailerMinLines)
{
    private const string Plugin = "cmd";

    /// <summary>
    /// Whether the printed output needs the trailer: above <paramref name="min"/> lines
    /// always; below it only when the shortening dropped something a reader could miss and
    /// the output does not already name the archive. An invalid UTF-8 body always lost
    /// bytes, since the filtered text cannot hold them.
    /// </summary>
    public static bool NeedsPointer(
        int lines,
        uint min,
        ReadOnlySpan<byte> body,
        string filtered,
        bool named
    )
    {
        if (lines > min)
        {
            return true;
        }

        if (
            Utf8.IsValid(body)
            && string.Equals(
                Canonical(Encoding.UTF8.GetString(body)),
                Canonical(filtered),
                StringComparison.Ordinal
            )
        )
        {
            return false;
        }

        return !named;
    }

    /// <summary>Whether <paramref name="filtered"/> already names the id (an inline <c>expand</c> marker).</summary>
    public static bool NamesTheId(string filtered, string id) =>
        filtered.Contains("expand " + id, StringComparison.Ordinal);

    /// <summary>The pointer line after a filtered body; one spelling for every surface.</summary>
    public static string Trailer(string id, int lines) =>
        $"[rtok {ArchiveIds.Abbreviate(id)} · {lines} lines]";

    /// <summary>
    /// <paramref name="text"/> up to the noise a filter may remove without a reader losing
    /// anything: whitespace runs and ANSI escapes, compared line by line.
    /// </summary>
    public static string Canonical(string text)
    {
        var output = new StringBuilder(text.Length);
        var line = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            switch (text[i])
            {
                case '\u001b':
                    i = Ansi.SkipEscape(text, i);
                    break;
                case '\n':
                    PushLine(line, output);
                    line.Clear();
                    i++;
                    break;
                case ' ' or '\t' or '\r':
                    while (i < text.Length && text[i] is ' ' or '\t' or '\r')
                    {
                        i++;
                    }

                    if (line.Length > 0)
                    {
                        line.Append(' ');
                    }

                    break;
                default:
                    line.Append(text[i++]);
                    break;
            }
        }

        PushLine(line, output);
        return output.ToString();
    }

    /// <summary>
    /// Writes what the host should see for <paramref name="body"/> to <paramref name="output"/>.
    /// With no host there is no archive to restore from, so the raw bytes go out (fail open).
    /// </summary>
    public void Emit(
        ICapabilities? host,
        IReadOnlyList<string> argv,
        byte[] body,
        int exit,
        string? agent,
        Stream output
    )
    {
        if (host is null)
        {
            WriteRaw(output, body);
            return;
        }

        // A same-window hit is a pointer, not the body; `agent` scopes it to the caller's window.
        if (IdenticalResult.Find(host, Plugin, body, agent) is { } pointer)
        {
            WriteText(output, pointer);
            return;
        }

        var before = Encoding.UTF8.GetString(body);
        var id = IdenticalResult.Sha256Hex(body);
        var lines = body.Length == 0 ? 0 : Text.Lines(before).Count;

        // No trailer on tiny outputs: filtered text plus a pointer can only be larger.
        if (body.Length <= Encoding.UTF8.GetByteCount(Trailer(id, lines)))
        {
            Raw(host, output, body, before);
            return;
        }

        var shown = Render(argv, before, body, exit, id, lines);

        if (WorseThanRaw(body, lines, shown))
        {
            Raw(host, output, body, before);
            return;
        }

        string? refId = null;
        if (shown.Archive)
        {
            // The archive keeps the command's bytes, not the lossy text: `expand` must return
            // what the command wrote, invalid UTF-8 included.
            if (Archive(host, body, agent) is not { } stored)
            {
                WriteRaw(output, body);
                return;
            }

            // The host names the archive; markers and the trailer must name the same id.
            if (!string.Equals(ArchiveIds.Abbreviate(stored), ArchiveIds.Abbreviate(id), StringComparison.Ordinal))
            {
                id = stored;
                shown = Render(argv, before, body, exit, id, lines);
            }

            refId = $"{Families.FamilyOf(settings, argv, before)}:{id}";
        }

        Filtered(host, output, body.Length, before, shown, refId);
    }

    private static void Filtered(
        ICapabilities host,
        Stream output,
        int bodyLength,
        string before,
        Shown shown,
        string? refId
    )
    {
        var bytes = Encoding.UTF8.GetBytes(shown.Text);
        output.Write(bytes);
        host.Record(
            new Measurement
            {
                Plugin = Plugin,
                Kind = shown.Kind,
                BeforeBytes = bodyLength,
                AfterBytes = bytes.Length,
                EstBefore = host.Estimate(Bounded.HostVisiblePrefix(before), TextClass.Code),
                EstAfter = host.Estimate(Bounded.HostVisiblePrefix(shown.Text), TextClass.Code),
                RefId = refId,
            }
        );
    }

    // Never worse than raw: a short body the filter made no shorter, or any output the
    // trailer would make longer, prints verbatim with no archive row.
    private bool WorseThanRaw(byte[] body, int lines, Shown shown) =>
        Utf8.IsValid(body)
        && (
            (lines <= trailerMinLines && shown.NoShorter)
            || Encoding.UTF8.GetByteCount(shown.Text) >= body.Length
        );

    private static void PushLine(StringBuilder line, StringBuilder output)
    {
        var t = line.ToString().Trim(' ', '\t', '\n', '\f', '\r');
        if (t.Length == 0)
        {
            return;
        }

        if (output.Length > 0)
        {
            output.Append('\n');
        }

        output.Append(t);
    }

    private static string? Archive(ICapabilities host, byte[] body, string? agent)
    {
        try
        {
            return host.PutArchiveFor(body, agent);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void Raw(ICapabilities host, Stream output, byte[] body, string before)
    {
        WriteRaw(output, body);
        var est = host.Estimate(Bounded.HostVisiblePrefix(before), TextClass.Code);
        host.Record(
            new Measurement
            {
                Plugin = Plugin,
                Kind = "raw",
                BeforeBytes = body.Length,
                AfterBytes = body.Length,
                EstBefore = est,
                EstAfter = est,
            }
        );
    }

    // The command's own bytes: the lossy text view would lose a replaced 0xff for good.
    private static void WriteRaw(Stream output, byte[] body)
    {
        output.Write(body);
        if (body.Length > 0 && body[^1] != (byte)'\n')
        {
            output.WriteByte((byte)'\n');
        }
    }

    private static void WriteText(Stream output, string text)
    {
        output.Write(Encoding.UTF8.GetBytes(text));
        if (text.Length > 0 && !text.EndsWith('\n'))
        {
            output.WriteByte((byte)'\n');
        }
    }

    // The filtered text, newline-padded, plus the trailer when the cut needs one.
    private Shown Render(
        IReadOnlyList<string> argv,
        string before,
        byte[] body,
        int exit,
        string id,
        int lines
    )
    {
        var shortId = ArchiveIds.Abbreviate(id);
        var (filtered, kind) = Families.Compress(settings, argv, before, exit, shortId);
        var named = NamesTheId(filtered, shortId);
        var pointer = trailer && NeedsPointer(lines, trailerMinLines, body, filtered, named);
        var noShorter =
            Encoding.UTF8.GetByteCount(filtered.TrimEnd())
            >= Encoding.UTF8.GetByteCount(before.TrimEnd());
        var text = new StringBuilder(filtered);
        if (text.Length > 0 && !filtered.EndsWith('\n'))
        {
            text.Append('\n');
        }

        if (pointer)
        {
            text.Append(Trailer(id, lines)).Append('\n');
        }

        return new Shown(text.ToString(), kind, pointer || named, noShorter);
    }

    private sealed record Shown(string Text, string Kind, bool Archive, bool NoShorter);
}
