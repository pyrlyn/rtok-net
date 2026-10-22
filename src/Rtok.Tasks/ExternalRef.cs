namespace Rtok.Tasks;

/// <summary>Where a remote adapter keeps a task.</summary>
/// <param name="Adapter"><c>github</c>, <c>gitlab</c> or <c>linear</c>.</param>
/// <param name="Number">The issue number (GitLab: the iid).</param>
/// <param name="Url">The issue's web page.</param>
/// <param name="NodeId">The GraphQL node id, when the service has one.</param>
public sealed record ExternalRef(string Adapter, long Number, string Url, string? NodeId = null);
