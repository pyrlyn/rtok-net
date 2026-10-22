namespace Rtok.Doctor;

/// <summary>What applying a confirmed plan did.</summary>
/// <param name="Text">The report, as <c>rtok doctor --fix --yes</c> prints it.</param>
/// <param name="Code">1 when a file was skipped or failed, or the plan changed; else 0.</param>
/// <param name="Replan">The fresh plan when the files changed since the preview; nothing was written then.</param>
public sealed record FixResult(string Text, int Code, FixPlan? Replan);
