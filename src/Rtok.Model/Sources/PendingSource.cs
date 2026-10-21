namespace Rtok.Model;

/// <summary>A page whose data has no source in this build yet: a fixed text, not ready.</summary>
/// <param name="text">What the page says.</param>
public sealed class PendingSource(string text) : IPageSource<TextPage>
{
    private readonly TextPage _page = new(text, Ready: false);

    /// <inheritdoc />
    public TextPage Read(ModelContext context) => _page;
}
