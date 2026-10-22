using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Rtok.Store;

namespace Rtok.Proxy;

/// <summary>Provider Batch result files as <c>usage</c> rows (rtok T385.4, T583;
/// <c>[proxy.batch] parse_results</c>).</summary>
/// <remarks>A results body is JSONL, one finished request per line. It is read only after the
/// bytes reached the client, so this is observation: a line that is not JSON, a request that
/// errored, expired or was cancelled, or a body without counters yields no row. Line shapes:
/// Anthropic <c>{"custom_id","result":{"type":"succeeded","message":{"model","usage"}}}</c>;
/// OpenAI <c>{"id","custom_id","response":{"status_code","body":{…,"usage"}},"error"}</c>, where
/// <c>body</c> is whatever the batched endpoint answers.</remarks>
internal static class BatchResults
{
    /// <summary>Whether a successful <c>GET</c> of <paramref name="path"/> on
    /// <paramref name="lane"/> is worth reading as a results file.</summary>
    public static BatchSource? Source(Lane lane, string method, string path)
    {
        if (!HttpMethods.IsGet(method))
        {
            return null;
        }
        return lane switch
        {
            Lane.Batch when path.EndsWith("/results", StringComparison.Ordinal) =>
                BatchSource.Batch,
            Lane.Files when path.EndsWith("/content", StringComparison.Ordinal) => BatchSource.File,
            _ => null,
        };
    }

    /// <summary>One row per line that carries usage. A line's key is
    /// <c>sha256(api \0 scope \0 id)</c>, so a results location fetched again adds nothing; a
    /// line without an id has no identity and is recorded each time.</summary>
    public static List<UsageInput> Rows(
        ReadOnlyMemory<byte> body,
        string scope,
        string? fallbackModel
    )
    {
        var rows = new List<UsageInput>();
        var rest = body;
        while (!rest.IsEmpty)
        {
            var end = rest.Span.IndexOf((byte)'\n');
            var line = end < 0 ? rest : rest[..end];
            rest = end < 0 ? ReadOnlyMemory<byte>.Empty : rest[(end + 1)..];
            using var doc = Json.Parse(line);
            if (doc is null || Line(doc.RootElement) is not { } found)
            {
                continue;
            }
            var (model, api, usage, id) = found;
            rows.Add(
                new UsageInput(
                    model ?? fallbackModel,
                    api,
                    usage.Input,
                    usage.CacheCreate,
                    usage.CacheRead,
                    usage.Output,
                    id is null ? null : Key(api, scope, id)
                )
            );
        }
        return rows;
    }

    internal static string Key(string api, string scope, string id) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{api}\0{scope}\0{id}")));

    private static (string? Model, string Api, Usage Usage, string? Id)? Line(JsonElement line)
    {
        if (Json.Get(line, "result") is { } result)
        {
            if (
                !string.Equals(Json.String(result, "type"), "succeeded", StringComparison.Ordinal)
                || Json.Get(result, "message") is not { } message
            )
            {
                return null;
            }
            // The Anthropic wire reads message.usage from the object that holds message.
            return AnthropicWire.Instance.UsageFromBody(result) is { } usage
                ? (
                    Json.String(message, "model"),
                    AnthropicWire.Instance.Api,
                    usage,
                    Id(line, "custom_id")
                )
                : null;
        }
        if (Json.Get(line, "response") is not { } response)
        {
            return null;
        }
        if (
            Json.Get(response, "status_code") is { ValueKind: JsonValueKind.Number } code
            && code.TryGetInt64(out var status)
            && status is < 200 or >= 300
        )
        {
            return null;
        }
        if (Json.Get(response, "body") is not { } body)
        {
            return null;
        }
        // The batched endpoint decides the shape: the Responses object says so, anything else
        // (Chat Completions, embeddings) reports prompt_tokens / completion_tokens.
        Wire wire = string.Equals(Json.String(body, "object"), "response", StringComparison.Ordinal)
            ? OpenAiResponsesWire.Instance
            : OpenAiChatWire.Instance;
        // OpenAI's id is unique per request line; custom_id is the caller's own label.
        return wire.UsageFromBody(body) is { } counters
            ? (Json.String(body, "model"), wire.Api, counters, Id(line, "id", "custom_id"))
            : null;
    }

    /// <summary>The first of <paramref name="fields"/> that is a non-empty string.</summary>
    private static string? Id(JsonElement line, params string[] fields) =>
        fields.Select(f => Json.String(line, f)).FirstOrDefault(s => s is not null);
}
