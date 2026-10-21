namespace Rtok.Fuzz;

/// <summary>
/// rtok's fuzz targets (rtok T314), by name. A target is wired by the task that ports the code it
/// feeds; until then it has no body.
/// </summary>
internal static class FuzzTargets
{
    public static readonly FuzzTarget[] All =
    [
        new("cli-argv", "T11", null),
        new("config-toml", "T7", null),
        new("cmd-filter", "T17", null),
        new("hook-io", "T13", null),
        new("jsonc-edit", "T12.3", null),
        new("proxy-wire", "T27", null),
        new("transcript-jsonl", "T30", null),
        new("outline", "T24", null),
        new("text-ops", "T19", null),
    ];
}
