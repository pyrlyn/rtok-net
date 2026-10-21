namespace Rtok.Store.Model;

/// <summary>Table <c>plugin_state</c>: one value a plugin keeps under its own key.</summary>
public class PluginState
{
    public string Plugin { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
