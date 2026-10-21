namespace Rtok.Config;

/// <summary>A command-line flag that sets a config key for one run.</summary>
/// <param name="Key">The dotted key.</param>
/// <param name="Value">The value, typed as the key's default.</param>
/// <param name="Flag">The flag as typed, for example <c>--port</c>.</param>
public sealed record ConfigFlag(string Key, object Value, string Flag);
