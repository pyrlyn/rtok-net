namespace Rtok.Hooks.Events;

/// <summary>One hook a host fires and the <c>rtok hook</c> event it runs.</summary>
/// <param name="Host">The host id, as <c>--host</c> spells it.</param>
/// <param name="HostEvent">The event name in the host's own config.</param>
/// <param name="RtokEvent">The word after <c>rtok hook</c>.</param>
/// <param name="Matcher">The tool matcher, empty when the entry has none.</param>
/// <param name="Installer">False for a row only the plugin manifest carries; the installer
/// skips it.</param>
public sealed record HookEventRow(
    string Host,
    string HostEvent,
    string RtokEvent,
    string Matcher,
    bool Installer
);
