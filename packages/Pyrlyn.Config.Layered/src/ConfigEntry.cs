namespace Pyrlyn.Config.Layered;

/// <summary>One effective leaf value and where it came from.</summary>
/// <param name="Key">The dotted key (<see cref="KeyPath"/>).</param>
/// <param name="Value">The value: string, long, double, bool, <see cref="Tomlyn.TomlDateTime"/>, or an array.</param>
/// <param name="Source">The layer that set it.</param>
public sealed record ConfigEntry(string Key, object Value, ConfigSource Source);
