using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Rtok.Testing;

namespace Rtok.Benchmarks;

// Sample: the cost of reading the hook payload's event name, the first thing every hook does.
[MemoryDiagnoser]
public class HookPayloadBenchmarks
{
    private byte[] _payload = [];

    [GlobalSetup]
    public void Setup() =>
        _payload = File.ReadAllBytes(RepoPaths.Fixture("hooks", "pre_tool_use.json"));

    [Benchmark]
    public string? EventName()
    {
        var reader = new Utf8JsonReader(_payload);
        while (reader.Read())
        {
            if (
                reader.TokenType == JsonTokenType.PropertyName
                && reader.ValueTextEquals("hook_event_name"u8)
            )
            {
                reader.Read();
                return reader.GetString();
            }
        }

        return null;
    }
}
