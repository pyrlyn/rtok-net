namespace Rtok.Config;

/// <summary>out-of-tree `.wasm` plugin host</summary>
/// <remarks>Read from [plugins.wasm].</remarks>
public sealed class WasmSection
{
    /// <summary>load out-of-tree WASM plugins</summary>
    public bool Enabled { get; set; }

    /// <summary>where WASM plugins live</summary>
    public string Dir { get; set; } = "~/.rtok/plugins";
}
