namespace Rtok.Tasks.Remote;

/// <summary>
/// Runs a helper such as <c>gh auth token</c> without a console and returns its trimmed
/// standard output, or null when it failed, printed nothing or is not installed. The surface
/// supplies it, since only the process runner may start processes.
/// </summary>
/// <param name="argv">The program and its arguments.</param>
/// <param name="ct">Cancels the run.</param>
/// <returns>The output, or null.</returns>
public delegate Task<string?> QuietRun(IReadOnlyList<string> argv, CancellationToken ct);
