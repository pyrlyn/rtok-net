namespace Rtok.Cli;

/// <summary>A provider whose own Batch API <c>rtok batch</c> calls.</summary>
internal enum BatchProvider
{
    /// <summary>Message Batches: <c>/v1/messages/batches</c>.</summary>
    Anthropic,

    /// <summary>Batch: <c>/v1/files</c> then <c>/v1/batches</c>.</summary>
    Openai,
}
