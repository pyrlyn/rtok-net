using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rtok.Tasks.Remote;

/// <summary>
/// JSON over HTTP for the remote adapters. Writes are paced apart; 403 and 429 rate limits are
/// waited out up to <see cref="MaxWait"/>; every failure names the method and URL, never the token.
/// </summary>
public sealed class RemoteClient
{
    /// <summary>How often a rate-limited request is sent.</summary>
    public const int Attempts = 3;

    /// <summary>The longest rate-limit wait before giving up.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly string _adapter;
    private readonly string _base;
    private readonly Action<HttpRequestMessage> _authorize;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _writeGap;
    private DateTimeOffset? _lastWrite;

    /// <summary>Creates a client.</summary>
    /// <param name="http">The HTTP client; its timeout applies.</param>
    /// <param name="adapter">The adapter name that starts every message: <c>github</c>.</param>
    /// <param name="baseUrl">The API base, without a trailing slash. Pagination never leaves it.</param>
    /// <param name="authorize">Adds the service's headers to each request.</param>
    /// <param name="clock">The clock for pacing and rate-limit resets.</param>
    /// <param name="writeGap">The least time between two writes: a second against the real services.</param>
    public RemoteClient(
        HttpClient http,
        string adapter,
        string baseUrl,
        Action<HttpRequestMessage> authorize,
        TimeProvider clock,
        TimeSpan writeGap
    )
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        _http = http;
        _adapter = adapter;
        _base = baseUrl.TrimEnd('/');
        _authorize = authorize;
        _clock = clock;
        _writeGap = writeGap;
    }

    /// <summary>The API base.</summary>
    public string Base => _base;

    /// <summary>Sends one request and parses the answer.</summary>
    /// <param name="method">The method.</param>
    /// <param name="url">The absolute URL.</param>
    /// <param name="body">The JSON body, or null.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The JSON answer; an empty answer is <c>null</c>.</returns>
    /// <exception cref="TaskException">The service refused or the answer is not JSON.</exception>
    public async Task<JsonElement> SendAsync(
        HttpMethod method,
        string url,
        string? body,
        CancellationToken ct = default
    ) => (await SendRawAsync(method, url, body, ct).ConfigureAwait(false)).Json;

    /// <summary>Every page of a GET that answers a JSON array, following <c>Link: rel="next"</c> within the base.</summary>
    /// <param name="url">The first page.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The items of all pages.</returns>
    public async Task<IReadOnlyList<JsonElement>> GetAllAsync(
        string url,
        CancellationToken ct = default
    )
    {
        var items = new List<JsonElement>();
        for (string? next = url; next is not null; )
        {
            var (json, link) = await SendRawAsync(HttpMethod.Get, next, null, ct)
                .ConfigureAwait(false);
            if (json.ValueKind != JsonValueKind.Array)
            {
                throw new TaskException($"{_adapter}: GET {next}: unexpected JSON");
            }
            items.AddRange(json.EnumerateArray());
            next = NextLink(link);
            if (next is not null && !next.StartsWith(_base + "/", StringComparison.Ordinal))
            {
                next = null;
            }
        }
        return items;
    }

    /// <summary>The <c>rel="next"</c> target of a <c>Link</c> header.</summary>
    /// <param name="link">The header value.</param>
    /// <returns>The URL, or null.</returns>
    public static string? NextLink(string? link)
    {
        if (string.IsNullOrEmpty(link))
        {
            return null;
        }
        foreach (var part in link.Split(','))
        {
            var pieces = part.Split(';');
            var target = pieces[0].Trim();
            if (
                target.StartsWith('<')
                && target.EndsWith('>')
                && pieces
                    .Skip(1)
                    .Any(p =>
                        p.Trim().Replace(" ", "", StringComparison.Ordinal)
                            is "rel=\"next\""
                                or "rel=next"
                    )
            )
            {
                return target[1..^1];
            }
        }
        return null;
    }

    /// <summary>
    /// How long a refused request should wait: <c>retry-after</c>, else the rate-limit reset when
    /// none remain, else a minute for 429. A 403 without those is a plain refusal.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="headers">The answer's headers.</param>
    /// <param name="now">Now, in Unix seconds.</param>
    /// <returns>The wait, or null when the refusal is not a rate limit.</returns>
    public static TimeSpan? RateWait(HttpStatusCode status, HttpResponseHeaders headers, long now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (status is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
        {
            return null;
        }
        if (
            Header(headers, "retry-after") is { } after
            && long.TryParse(
                after,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var seconds
            )
        )
        {
            return TimeSpan.FromSeconds(seconds);
        }
        if (
            string.Equals(Header(headers, "x-ratelimit-remaining"), "0", StringComparison.Ordinal)
            && Header(headers, "x-ratelimit-reset") is { } reset
            && long.TryParse(reset, NumberStyles.None, CultureInfo.InvariantCulture, out var at)
        )
        {
            return TimeSpan.FromSeconds(Math.Max(0, at - now));
        }
        return status == HttpStatusCode.TooManyRequests ? TimeSpan.FromSeconds(60) : null;
    }

    private static string? Header(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? values.FirstOrDefault()?.Trim() : null;

    private async Task<(JsonElement Json, string? Link)> SendRawAsync(
        HttpMethod method,
        string url,
        string? body,
        CancellationToken ct
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            if (body is not null)
            {
                await PaceAsync(ct).ConfigureAwait(false);
            }
            using var request = new HttpRequestMessage(method, url);
            _authorize(request);
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                throw new TaskException($"{_adapter}: {method} {url}: {e.Message}", e);
            }
            catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
            {
                throw new TaskException($"{_adapter}: {method} {url}: timed out", e);
            }
            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var link = response.Headers.TryGetValues("Link", out var links)
                        ? string.Join(',', links)
                        : null;
                    return (Parse(method, url, text), link);
                }
                var wait = RateWait(
                    response.StatusCode,
                    response.Headers,
                    _clock.GetUtcNow().ToUnixTimeSeconds()
                );
                if (wait is { } w)
                {
                    if (w > MaxWait || attempt >= Attempts)
                    {
                        throw new TaskException(
                            $"{_adapter}: {method} {url}: rate limited; try again in {(long)Math.Ceiling(w.TotalSeconds)} s"
                        );
                    }
                    await Task.Delay(w, _clock, ct).ConfigureAwait(false);
                    continue;
                }
                throw new TaskException(
                    $"{_adapter}: {method} {url}: {Status(response)}: {Message(text)}"
                );
            }
        }
    }

    private async Task PaceAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        if (_lastWrite is { } last && now - last < _writeGap)
        {
            await Task.Delay(_writeGap - (now - last), _clock, ct).ConfigureAwait(false);
        }
        _lastWrite = _clock.GetUtcNow();
    }

    private JsonElement Parse(HttpMethod method, string url, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new TaskException($"{_adapter}: {method} {url}: unexpected JSON", e);
        }
    }

    private static string Status(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        var reason = string.IsNullOrEmpty(response.ReasonPhrase)
            ? response.StatusCode.ToString()
            : response.ReasonPhrase;
        return $"{code.ToString(CultureInfo.InvariantCulture)} {reason}";
    }

    /// <summary>The service's own words: <c>message</c>, else <c>errors[0].message</c>, else the start of the body.</summary>
    private static string Message(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (
                    root.TryGetProperty("message", out var m)
                    && m.ValueKind == JsonValueKind.String
                )
                {
                    return m.GetString()!;
                }
                if (
                    root.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Array
                    && errors.GetArrayLength() > 0
                    && errors[0].ValueKind == JsonValueKind.Object
                    && errors[0].TryGetProperty("message", out var first)
                    && first.ValueKind == JsonValueKind.String
                )
                {
                    return first.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: the body itself.
        }
        var trimmed = text.Trim();
        return trimmed.Length <= 300 ? trimmed : trimmed[..300];
    }
}
