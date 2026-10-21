namespace Rtok.Plugin.Sdk;

/// <summary>
/// Text offered for the model's context. The host emits offers in priority order until its
/// per-turn budget is spent (rtok D5).
/// </summary>
/// <param name="Plugin">Catalogue id of the offering plugin.</param>
/// <param name="Text">The text; it must be byte-stable across turns, or it costs a prompt-cache
/// miss on every one of them.</param>
/// <param name="Priority">Higher first, 0 to 255.</param>
public sealed record Injection(string Plugin, string Text, int Priority);
