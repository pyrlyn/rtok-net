namespace Rtok.Log;

/// <summary>
/// One step of a watch: <paramref name="Fresh"/> is what a pipe prints now, and
/// <paramref name="Screen"/> is what a terminal shows once the step lands.
/// </summary>
/// <param name="Fresh">The rows to append when piped.</param>
/// <param name="Screen">The whole screen to repaint on a terminal.</param>
public sealed record WatchTick(IReadOnlyList<string> Fresh, IReadOnlyList<string> Screen);
