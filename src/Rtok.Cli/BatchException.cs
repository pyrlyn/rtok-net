namespace Rtok.Cli;

/// <summary>A batch request refused before or by the provider; the message is for the user.</summary>
internal sealed class BatchException(string message) : Exception(message);
