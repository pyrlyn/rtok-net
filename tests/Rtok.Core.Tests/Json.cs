using System.Text.Json;

namespace Rtok.Core.Tests;

internal static class Json
{
    public static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
