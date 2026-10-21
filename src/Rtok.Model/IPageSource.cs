namespace Rtok.Model;

/// <summary>Reads one page of the snapshot. Each page has its own source, so a page whose data
/// is not built yet takes a stand-in until its task wires the real one.</summary>
/// <typeparam name="TPage">The page.</typeparam>
public interface IPageSource<out TPage>
{
    /// <summary>Reads the page for one build.</summary>
    /// <param name="context">What the build sees: one config and one clock.</param>
    /// <returns>The page.</returns>
    TPage Read(ModelContext context);
}
