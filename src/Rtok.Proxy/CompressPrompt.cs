using System.Security.Cryptography;
using System.Text;
using Pyrlyn.Config.Layered;

namespace Rtok.Proxy;

/// <summary>The compressor prompt: data, not code (<c>compress-prompt.toml</c>).</summary>
internal static class CompressPrompt
{
    private const int VersionHashChars = 12;

    private static readonly byte[] File = Load();

    /// <summary>The system prompt.</summary>
    public static readonly string System = Field("system");

    /// <summary>The user template, with <c>{{must_keep}}</c> and <c>{{text}}</c>.</summary>
    public static readonly string User = Field("user");

    /// <summary>The key half beside the archive id: the prompt file's hash and the model, so
    /// editing the prompt or changing the model invalidates every stored summary.</summary>
    public static string Version(string model) =>
        $"{Convert.ToHexStringLower(SHA256.HashData(File))[..VersionHashChars]}+{model}";

    /// <summary>The user message for <paramref name="text"/>. The template is split before it is
    /// filled, so a tool result that contains <c>{{must_keep}}</c> stays text.</summary>
    public static string Fill(string text, IEnumerable<MustKeepSpan> spans)
    {
        var mustKeep = string.Join('\n', spans.Select(s => $"- {s.Text}"));
        var at = User.IndexOf("{{text}}", StringComparison.Ordinal);
        var (head, tail) = at < 0 ? (User, "") : (User[..at], User[(at + "{{text}}".Length)..]);
        return head.Replace("{{must_keep}}", mustKeep, StringComparison.Ordinal) + text + tail;
    }

    private static byte[] Load()
    {
        using var stream =
            typeof(CompressPrompt).Assembly.GetManifestResourceStream(
                "Rtok.Proxy.compress-prompt.toml"
            ) ?? throw new InvalidOperationException("compress-prompt.toml is not embedded");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string Field(string key) =>
        TomlText.Parse(Encoding.UTF8.GetString(File), "compress-prompt.toml")[key] is string s
            ? s.Trim()
            : throw new InvalidOperationException($"compress-prompt.toml: {key} is not a string");
}
