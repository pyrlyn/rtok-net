namespace Rtok.Process.Tests;

/// <summary>The display is process state, so the tests that install one run alone.</summary>
[CollectionDefinition(nameof(DisplayState), DisableParallelization = true)]
public sealed class DisplayState;
