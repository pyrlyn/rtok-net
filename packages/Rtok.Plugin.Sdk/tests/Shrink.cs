namespace Rtok.Plugin.Sdk.Tests;

internal static class Shrink
{
    public static Measurement Measure(string plugin, string kind, long before, long after) =>
        new()
        {
            Plugin = plugin,
            Kind = kind,
            BeforeBytes = before,
            AfterBytes = after,
            EstBefore = (int)before / 4,
            EstAfter = (int)after / 4,
        };
}
