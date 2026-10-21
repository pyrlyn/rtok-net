namespace Rtok.Plugin.Sdk;

/// <summary>One stored observation, as a search returns it.</summary>
/// <param name="Id">Observation id; pass it to <see cref="INotes.ObservationNarrative"/>.</param>
/// <param name="Title">Tool name, capped.</param>
/// <param name="SessionId">Host session that produced it.</param>
/// <param name="Snippet">A short excerpt of the narrative.</param>
public sealed record ObsHit(long Id, string Title, string SessionId, string Snippet);
