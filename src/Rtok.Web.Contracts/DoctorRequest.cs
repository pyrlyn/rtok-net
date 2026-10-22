namespace Rtok.Web.Contracts;

/// <summary>The <c>doctor --fix</c> checklist on the Doctor page.</summary>
/// <param name="Action"><c>plan</c> returns the items and the diff and writes nothing;
/// <c>apply</c> writes the plan the user confirmed.</param>
/// <param name="Selection">The item ids to fix; empty means every fixable item.</param>
/// <param name="Confirmed">For <c>apply</c>: the token of the confirmed plan. A token the files
/// no longer produce writes nothing and returns a fresh plan.</param>
public sealed record DoctorRequest(
    string Action,
    IReadOnlyList<string>? Selection = null,
    string? Confirmed = null
);
