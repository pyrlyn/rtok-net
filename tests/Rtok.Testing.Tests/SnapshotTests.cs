using Rtok.Testing;

namespace Rtok.Testing.Tests;

public sealed class SnapshotTests
{
    // Verify sample: the hook fixture set is a snapshot; a change shows as a .received file.
    [Fact]
    public Task Hook_fixture_names_match_the_snapshot() =>
        Verify(
            Directory
                .EnumerateFiles(RepoPaths.Fixture("hooks"), "*.json")
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
        );
}
