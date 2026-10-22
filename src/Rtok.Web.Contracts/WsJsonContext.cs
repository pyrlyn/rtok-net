using System.Text.Json.Serialization;

namespace Rtok.Web.Contracts;

/// <summary>The one STJ context for the <c>/ws</c> wire, source-generated for AOT and the
/// trimmed browser bundle; the host and the client both use it.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    // A message missing a field, or null where the contract has none, is refused whole; so
    // every field is written, nulls included.
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true
)]
[JsonSerializable(typeof(WsRequest))]
[JsonSerializable(typeof(ServerFrame))]
[JsonSerializable(typeof(Snapshot))]
public sealed partial class WsJsonContext : JsonSerializerContext;
