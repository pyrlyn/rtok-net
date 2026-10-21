using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Rtok.AotTests;

// Tests here pass only when they run as the NativeAOT executable, so a green run proves the lane
// really runs AOT code.
public sealed class RuntimeTests
{
    // A NativeAOT image has no assembly files; on the JIT Location is the dll's path.
#pragma warning disable IL3000 // the empty Location is the point of this test
    [Fact]
    public void Runs_as_native_code() => Assert.Empty(typeof(RuntimeTests).Assembly.Location);
#pragma warning restore IL3000

    [Fact]
    public void Runs_without_dynamic_code() => Assert.False(RuntimeFeature.IsDynamicCodeSupported);

    [Fact]
    public void Source_generated_json_round_trips()
    {
        var json = JsonSerializer.Serialize(
            new Probe("PreToolUse", 3),
            AotTestsJsonContext.Default.Probe
        );

        Assert.Equal("""{"event":"PreToolUse","count":3}""", json);
        Assert.Equal(
            new Probe("PreToolUse", 3),
            JsonSerializer.Deserialize(json, AotTestsJsonContext.Default.Probe)
        );
    }
}
