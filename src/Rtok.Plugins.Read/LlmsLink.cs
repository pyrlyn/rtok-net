namespace Rtok.Plugins.Read;

/// <summary>One list link of an <c>llms.txt</c>. The URL is recorded, never fetched.</summary>
/// <param name="Title">The link text.</param>
/// <param name="Url">The target.</param>
/// <param name="Description">The note after <c>:</c>, or empty.</param>
public sealed record LlmsLink(string Title, string Url, string Description);
