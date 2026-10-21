namespace Rtok.Plugin.Sdk;

/// <summary>What a plugin declares about itself.</summary>
/// <param name="Id">Catalogue id such as <c>cmd</c>: also the <c>[plugins.&lt;id&gt;]</c>
/// config key.</param>
/// <param name="Surfaces">The surfaces this plugin answers on; everything else keeps the
/// no-op defaults of <see cref="IPlugin"/>.</param>
/// <param name="DefaultOn">Enabled unless <c>[plugins.&lt;id&gt;] enabled</c> says
/// otherwise.</param>
public sealed record Manifest(string Id, Surface Surfaces, bool DefaultOn);
