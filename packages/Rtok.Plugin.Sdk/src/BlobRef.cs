namespace Rtok.Plugin.Sdk;

/// <summary>
/// One shrinkable non-result payload in the live zone: a nested JSON dump or a <c>data:</c>
/// blob inside a user content block. It has no provider-stable id, so a plugin keys its
/// decision by content hash, which also keeps the rewrite byte-stable across turns.
/// </summary>
/// <param name="Content">The string payload; rewrite it in place.</param>
/// <param name="Turn">How many user turns follow this block.</param>
public sealed record BlobRef(JsonSlot Content, int Turn);
