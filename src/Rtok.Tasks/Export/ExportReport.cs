namespace Rtok.Tasks.Export;

/// <summary>What <c>rtok task export</c> found or did.</summary>
/// <param name="Backend"><c>github</c> or <c>linear</c>.</param>
/// <param name="Target">Where the issues go: <c>owner/name</c> or a team key.</param>
/// <param name="DryRun">Nothing was sent.</param>
/// <param name="Actions">The writes, in id order.</param>
/// <param name="Unchanged">Tasks whose issue already matches.</param>
/// <param name="Skipped">Finished tasks with no issue: history is not exported.</param>
public sealed record ExportReport(
    string Backend,
    string Target,
    bool DryRun,
    IReadOnlyList<ExportAction> Actions,
    int Unchanged,
    int Skipped
);
