using System.Text;
using System.Text.Unicode;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Inject;

/// <summary>
/// Prompt modes as data (rtok D7): <c>modes/&lt;name&gt;.md</c>, each at most 250 tokens. A file
/// in <c>plugins.inject.modes_dir</c> wins over the built-in mode of the same name; the aliases
/// <c>cave</c> (terse) and <c>pony</c> (yagni) name the built-ins too.
/// </summary>
public static class ModeFiles
{
    /// <summary>The modes shipped with rtok.</summary>
    public static IReadOnlyList<string> Shipped { get; } = ["terse", "yagni", "nudges"];

    /// <summary>The built-in text of <paramref name="name"/> or one of its aliases, or
    /// <see langword="null"/> for a name rtok does not ship.</summary>
    public static string? Builtin(string name) =>
        name switch
        {
            "terse" or "cave" => Embedded("terse"),
            "yagni" or "pony" => Embedded("yagni"),
            "nudges" => Embedded("nudges"),
            _ => null,
        };

    /// <summary>
    /// Every configured mode (<c>plugins.inject.modes</c>, else <c>setup.modes</c>), each
    /// fitted to the inject budget and ending in a line break; empty when none is configured or
    /// none can be read.
    /// </summary>
    public static string Text(Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(cx);
        var config = cx.PluginConfig("inject", InjectJsonContext.Default.InjectConfig);
        var names =
            config.Modes.Count > 0
                ? config.Modes
                : cx.Config("setup", InjectJsonContext.Default.SetupConfig).Modes;
        if (names.Count == 0)
        {
            return "";
        }

        var budget = InjectBudget.Tokens(cx.Host);
        var rate = Math.Max(EstimatorRates.From(cx.Host.ConfigJson("estimator")).Prose, 0.1f);
        var text = new StringBuilder();
        foreach (var name in names)
        {
            if (Read(PathOf(config.ModesDir, name), Builtin(name), budget, rate) is not { } body)
            {
                continue;
            }

            body = InjectBudget.Fit(cx.Host, body, TextClass.Prose, budget);
            text.Append(body);
            if (!body.EndsWith('\n'))
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// The mode at <paramref name="path"/>, read capped: the estimator budgets characters, so
    /// it keeps <c>ceil(budget·rate)+1</c> of them (one past what fits, so the fitter still sees
    /// the overshoot) and reads at most four bytes for each. A partial UTF-8 tail is dropped.
    /// A file that cannot be opened or read gives <paramref name="builtin"/>; nothing throws.
    /// </summary>
    public static string? Read(string? path, string? builtin, int budget, float rate)
    {
        if (path is null)
        {
            return builtin;
        }

        var chars = (long)Math.Ceiling((double)Math.Max(budget, 0) * rate) + 1;
        byte[] bytes;
        try
        {
            using var file = File.OpenHandle(path);
            var length = RandomAccess.GetLength(file);
            bytes = new byte[(int)Math.Min(Math.Min(chars * 4, length), Array.MaxLength)];
            var read = 0;
            while (read < bytes.Length)
            {
                var n = RandomAccess.Read(file, bytes.AsSpan(read), read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            bytes = bytes[..read];
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or NotSupportedException
                        or ArgumentException
            )
        {
            return builtin;
        }

        return Decode(bytes, chars);
    }

    // The first `chars` scalars of `bytes`: invalid sequences become U+FFFD as in a full
    // decode, and a sequence the cap cut short is dropped rather than replaced, so the result
    // is always a prefix of the whole file decoded.
    internal static string Decode(ReadOnlySpan<byte> bytes, long chars)
    {
        var utf16 = new char[bytes.Length];
        Utf8.ToUtf16(bytes, utf16, out _, out var written, isFinalBlock: false);
        var text = utf16.AsSpan(0, written);
        var length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (chars-- == 0)
            {
                break;
            }

            length += rune.Utf16SequenceLength;
        }

        return new string(text[..length]);
    }

    // A mode name is a file name in modes_dir, never a path out of it.
    private static string? PathOf(string? dir, string name) =>
        string.IsNullOrEmpty(dir)
        || name.Length == 0
        || name is "." or ".."
        || name.AsSpan().IndexOfAny(['/', '\\', '\0']) >= 0
            ? null
            : Path.Join(dir, name + ".md");

    private static string Embedded(string name)
    {
        using var stream = typeof(ModeFiles).Assembly.GetManifestResourceStream(
            $"Rtok.Plugins.Inject.modes.{name}.md"
        )!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
