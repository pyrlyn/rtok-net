using System.Text;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Cmd;

/// <summary><c>rtok filter --stdin [--cmd]</c>: compresses text as if it were the output of a command, without running it.</summary>
internal sealed class StdinFilter(CmdSettings settings, bool trailer, uint trailerMinLines)
{
    /// <summary>
    /// Archives (when the shortening dropped something), compresses, adds the trailer and
    /// records a Measurement. With no host, or when the archive fails, it only compresses.
    /// Never worse than raw.
    /// </summary>
    public string Run(string cmd, string input, ICapabilities? host)
    {
        var argv = Text.Words(cmd);
        if (host is null)
        {
            return CompressOnly(argv, input);
        }

        var body = Encoding.UTF8.GetBytes(input);
        var lines = input.Length == 0 ? 0 : Text.Lines(input).Count;
        var id = IdenticalResult.Sha256Hex(body);
        var shown = Render(argv, input, body, lines, id);
        if (shown is null)
        {
            return input;
        }

        string? refId = null;
        if (shown.Archive)
        {
            if (Archive(host, body) is not { } stored)
            {
                return CompressOnly(argv, input);
            }

            // The host names the archive; markers and the trailer must name the same id.
            if (!string.Equals(Names.ShortId(stored), Names.ShortId(id), StringComparison.Ordinal))
            {
                id = stored;
                shown = Render(argv, input, body, lines, id) ?? shown;
            }

            refId = $"{Families.FamilyOf(settings, argv, input)}:{id}";
        }

        host.Record(
            new Measurement
            {
                Plugin = "cmd",
                Kind = shown.Kind,
                BeforeBytes = body.Length,
                AfterBytes = Encoding.UTF8.GetByteCount(shown.Text),
                EstBefore = host.Estimate(input, TextClass.Code),
                EstAfter = host.Estimate(shown.Text, TextClass.Code),
                RefId = refId,
            }
        );
        return shown.Text;
    }

    private static string? Archive(ICapabilities host, byte[] body)
    {
        try
        {
            return host.PutArchive(body);
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

    // Never worse than raw here either: text the cut does not shorten passes through as is.
    private string CompressOnly(string[] argv, string input)
    {
        var text = Families.Compress(settings, argv, input, 0, "").Text;
        return
            Encoding.UTF8.GetByteCount(text.TrimEnd())
            >= Encoding.UTF8.GetByteCount(input.TrimEnd())
            ? input
            : text;
    }

    // The compressed text plus the trailer when it needs one; null when raw is no longer.
    private Shown? Render(string[] argv, string input, byte[] body, int lines, string id)
    {
        var (filtered, kind) = Families.Compress(settings, argv, input, 0, Names.ShortId(id));
        var named = Emitter.NamesTheId(filtered, Names.ShortId(id));
        var pointer =
            trailer && Emitter.NeedsPointer(lines, trailerMinLines, body, filtered, named);
        if (
            lines <= trailerMinLines
            && Encoding.UTF8.GetByteCount(filtered.TrimEnd())
                >= Encoding.UTF8.GetByteCount(input.TrimEnd())
        )
        {
            return null;
        }

        var output = new StringBuilder(filtered);
        if (pointer)
        {
            if (output.Length > 0 && !filtered.EndsWith('\n'))
            {
                output.Append('\n');
            }

            output.Append(Emitter.Trailer(id, lines)).Append('\n');
        }

        var text = output.ToString();
        return Encoding.UTF8.GetByteCount(text) >= body.Length
            ? null
            : new Shown(text, kind, pointer || named);
    }

    private sealed record Shown(string Text, string Kind, bool Archive);
}
