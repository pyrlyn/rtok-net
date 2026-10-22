using System.Text.Json.Serialization;

namespace Rtok.Web.Contracts;

/// <summary>One frame the server sends over <c>/ws</c>, tagged by <c>type</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SnapshotFrame), "snapshot")]
[JsonDerivedType(typeof(MessageFrame), "message")]
[JsonDerivedType(typeof(DoneFrame), "done")]
[JsonDerivedType(typeof(ExpandFrame), "expand")]
public abstract record ServerFrame
{
    /// <summary>The id of the request this frame answers; null for a pushed frame.</summary>
    public long? Req { get; init; }
}
