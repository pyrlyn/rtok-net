using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text as a host's strict JSON config through <see cref="HostFile"/>: parsing fails only
/// with <see cref="InvalidDataException"/>, and what it parses writes back to an equal tree.
/// </summary>
internal static class HostJson
{
    private const string Path = "/fuzz/.host/settings.json";

    public static void Run(ReadOnlySpan<byte> data)
    {
        JsonNode node;
        try
        {
            node = HostFile.ParseStrictJson(Path, FuzzInput.Text(data));
        }
        catch (InvalidDataException)
        {
            return;
        }
        var again = HostFile.ParseStrictJson(Path, HostFile.ToJson(node));
        FuzzInput.Check(
            JsonNode.DeepEquals(again, node),
            "ToJson does not parse back to the same tree"
        );
        _ = Ownership.RunsBin(node, _ => true);
        _ = Ownership.WithoutDefaultType(node);
    }
}
