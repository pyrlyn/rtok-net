namespace Rtok.Tasks.GitLab;

/// <summary>A GitLab project as messages name it and as URLs carry it.</summary>
/// <param name="Path"><c>group/name</c> or a numeric id.</param>
/// <param name="Encoded"><c>group%2Fname</c> or the id.</param>
internal sealed record GitLabProjectRef(string Path, string Encoded);
