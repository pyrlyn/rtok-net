using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Rtok.Agents.Usage;
using Rtok.Config;
using Rtok.Plugin.Sdk;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok stats --calibrate</c>: tool results from the transcripts in the window, sorted by
/// text class, counted by Anthropic's <c>count_tokens</c>; chars per token per class is written
/// to <c>[estimator]</c> in the user config. Without a key it is skipped, and any failure leaves
/// the stored rates as they were: nothing is written until every count is in.
/// </summary>
public static class Calibrate
{
    /// <summary>The public endpoint; tests point the client elsewhere.</summary>
    public static readonly Uri Endpoint = new("https://api.anthropic.com/v1/messages/count_tokens");

    /// <summary>The model whose tokenizer the rates follow.</summary>
    public const string Model = "claude-sonnet-5-5";

    /// <summary>A sample's cap, so one huge result costs a bounded request.</summary>
    private const int MaxSampleChars = 8_000;

    private const int MinSampleChars = 200;

    /// <summary>Fits and writes the rates; returns the line <c>rtok stats --calibrate</c> prints.</summary>
    /// <param name="config">The loaded config: the window, the transcripts and the sample count.</param>
    /// <param name="userFile">The user config file the rates go to.</param>
    /// <param name="apiKey">The Anthropic key; null or empty skips.</param>
    /// <param name="http">The client; its base address is ignored, <paramref name="endpoint"/> is used.</param>
    /// <param name="endpoint">The <c>count_tokens</c> URL.</param>
    /// <param name="now">The window's end.</param>
    /// <param name="cancellationToken">Stops the requests.</param>
    public static async Task<string> RunAsync(
        RtokConfig config,
        string userFile,
        string? apiKey,
        HttpClient http,
        Uri endpoint,
        DateTime now,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(http);
        if (string.IsNullOrEmpty(apiKey))
        {
            return "skipped: no ANTHROPIC_API_KEY\n";
        }
        var cutoff = now - StatsPage.ParseSince(config.Stats.Since, "stats.since");
        var samples = Samples(
            config.Stats.TranscriptsDir,
            cutoff,
            (int)config.Stats.CalibrateSamples
        );
        if (samples.Count == 0)
        {
            return "skipped: no tool results to sample\n";
        }
        Dictionary<TextClass, double> fitted;
        try
        {
            fitted = await FitAsync(samples, apiKey, http, endpoint, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception e)
            when (e
                    is HttpRequestException
                        or TaskCanceledException
                        or JsonException
                        or InvalidOperationException
            )
        {
            return $"calibrate failed, [estimator] unchanged: {e.Message}\n";
        }
        var sb = new StringBuilder();
        foreach (var (cls, rate) in fitted)
        {
            var key = "estimator." + cls.ToString().ToLowerInvariant();
            var value = rate.ToString("0.00", CultureInfo.InvariantCulture);
            ConfigFile.Set(userFile, key, value);
            sb.Append(
                CultureInfo.InvariantCulture,
                $"{key} = {value} ({samples[cls].Count} samples)\n"
            );
        }
        return sb.ToString();
    }

    /// <summary>
    /// Up to <paramref name="perClass"/> results of each class, distinct, in transcript order;
    /// short ones say little about a rate and are left out.
    /// </summary>
    internal static Dictionary<TextClass, List<string>> Samples(
        string dir,
        DateTime cutoff,
        int perClass
    )
    {
        var picked = new Dictionary<TextClass, List<string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in LogFiles.Find(dir, cutoff, "jsonl"))
        {
            if (Transcript.Read(path) is not { } t)
            {
                continue;
            }
            foreach (var r in t.ToolResults)
            {
                var text =
                    r.Content.Length > MaxSampleChars ? r.Content[..MaxSampleChars] : r.Content;
                if (text.Length < MinSampleChars || !seen.Add(text))
                {
                    continue;
                }
                var cls = Classify(text);
                if (!picked.TryGetValue(cls, out var list))
                {
                    list = [];
                    picked[cls] = list;
                }
                if (list.Count < perClass)
                {
                    list.Add(text);
                }
            }
        }
        return picked;
    }

    /// <summary>JSON when it parses as an object or array, CJK when a third of the letters are,
    /// prose when code punctuation is rare, else code.</summary>
    internal static TextClass Classify(string text)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length > 0 && trimmed[0] is '{' or '[' && IsJson(trimmed))
        {
            return TextClass.Json;
        }
        var (letters, cjk, symbols) = (0, 0, 0);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                letters++;
                if (IsCjk(rune.Value))
                {
                    cjk++;
                }
            }
            else if (
                rune.Value
                is '{'
                    or '}'
                    or ';'
                    or '='
                    or '<'
                    or '>'
                    or '('
                    or ')'
                    or '['
                    or ']'
                    or '$'
                    or '|'
            )
            {
                symbols++;
            }
        }
        return letters > 0 && cjk * 3 >= letters ? TextClass.Cjk
            : symbols * 50 < text.Length ? TextClass.Prose
            : TextClass.Code;
    }

    /// <summary>Chars per token per class: every sample counted, less the request's own tokens.</summary>
    private static async Task<Dictionary<TextClass, double>> FitAsync(
        Dictionary<TextClass, List<string>> samples,
        string apiKey,
        HttpClient http,
        Uri endpoint,
        CancellationToken ct
    )
    {
        // One character's request: what the envelope costs beyond the text.
        var overhead = await CountAsync("a", apiKey, http, endpoint, ct).ConfigureAwait(false) - 1;
        var fitted = new Dictionary<TextClass, double>();
        foreach (var (cls, texts) in samples.OrderBy(p => p.Key))
        {
            long chars = 0;
            long tokens = 0;
            foreach (var text in texts)
            {
                chars += text.EnumerateRunes().Count();
                tokens += Math.Max(
                    await CountAsync(text, apiKey, http, endpoint, ct).ConfigureAwait(false)
                        - overhead,
                    1
                );
            }
            fitted[cls] = Math.Round((double)chars / tokens, 2);
        }
        return fitted;
    }

    private static async Task<long> CountAsync(
        string text,
        string apiKey,
        HttpClient http,
        Uri endpoint,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(Body(text))
            {
                Headers = { ContentType = new("application/json") },
            },
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(bytes);
        return
            doc.RootElement.ValueKind is JsonValueKind.Object
            && doc.RootElement.TryGetProperty("input_tokens", out var n)
            && n.TryGetInt64(out var tokens)
            ? tokens
            : throw new InvalidOperationException("count_tokens answered without input_tokens");
    }

    private static byte[] Body(string text)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("model", Model);
            w.WriteStartArray("messages");
            w.WriteStartObject();
            w.WriteString("role", "user");
            w.WriteString("content", text);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static bool IsJson(ReadOnlySpan<char> text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text.ToString());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsCjk(int c) =>
        c
            is (>= 0x3040 and <= 0x30FF)
                or (>= 0x3400 and <= 0x4DBF)
                or (>= 0x4E00 and <= 0x9FFF)
                or (>= 0xAC00 and <= 0xD7AF);
}
