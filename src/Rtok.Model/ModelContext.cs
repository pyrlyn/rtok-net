using Rtok.Config;

namespace Rtok.Model;

/// <summary>What one build reads from: the config taken once for the whole build, and the clock.</summary>
/// <param name="Config">The loaded config.</param>
/// <param name="Time">The clock.</param>
public sealed record ModelContext(LoadedConfig Config, TimeProvider Time);
