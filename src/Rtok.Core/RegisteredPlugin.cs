using Rtok.Plugin.Sdk;

namespace Rtok.Core;

/// <summary>A linked plugin as the registry sees it.</summary>
/// <param name="Plugin">The plugin.</param>
/// <param name="Kind"><c>builtin</c> for a <see cref="Catalogue"/> id, <c>external</c> for a
/// plugin built outside the repository.</param>
/// <param name="Enabled">Whether it runs: <c>[plugins.&lt;id&gt;] enabled</c>, else its
/// manifest's default.</param>
public sealed record RegisteredPlugin(IPlugin Plugin, string Kind, bool Enabled);
