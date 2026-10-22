namespace Rtok.Doctor;

/// <summary>What <c>--fix</c> should look at and how the checklist was changed from its defaults.</summary>
public sealed record FixRequest
{
    /// <summary>The problem kinds to fix (<see cref="ProblemKinds.Fixed"/>); null for all of them.</summary>
    public IReadOnlyList<string>? Kinds { get; init; }

    /// <summary>Copies of duplicates to keep instead of the recommended one, in order.</summary>
    public IReadOnlyList<FixRef> Keep { get; init; } = [];

    /// <summary>Entries whose selection is flipped from the default.</summary>
    public IReadOnlyList<FixRef> Toggled { get; init; } = [];
}
