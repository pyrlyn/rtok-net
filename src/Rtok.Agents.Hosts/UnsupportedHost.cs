namespace Rtok.Agents.Hosts;

/// <summary>
/// A host rtok writes nothing into, with why and where that was last checked: install,
/// uninstall, update and info refuse it by name.
/// </summary>
/// <param name="Id">The id it is asked for by: <c>trae</c>.</param>
/// <param name="Name">The app's own name.</param>
/// <param name="Kind">CLI or desktop.</param>
/// <param name="Checked">The day <paramref name="Sources"/> were last read.</param>
/// <param name="Sources">The host's own docs the reason comes from.</param>
/// <param name="Reason">Why there is no installer, as one clause.</param>
public sealed record UnsupportedHost(
    string Id,
    string Name,
    HostKind Kind,
    DateOnly Checked,
    IReadOnlyList<string> Sources,
    string Reason
);
