namespace Rtok.Web.Contracts;

/// <summary>Turn a linked plugin on or off: <c>plugins.&lt;id&gt;.enabled</c>, written as
/// <c>rtok config set</c> writes it.</summary>
/// <param name="Plugin">The plugin id.</param>
/// <param name="Enabled">The new state.</param>
public sealed record ToggleRequest(string Plugin, bool Enabled);
