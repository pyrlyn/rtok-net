using Rtok.Testing;

namespace Rtok.Hooks.Tests.Pipeline;

internal static class Fixtures
{
    public static string Hook(string name) =>
        File.ReadAllText(RepoPaths.Fixture("hooks", name)).TrimEnd('\n');
}
