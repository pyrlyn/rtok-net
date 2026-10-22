namespace Rtok.Proxy;

/// <summary>Where a response may hold a provider Batch results file.</summary>
internal enum BatchSource
{
    /// <summary>Anthropic <c>…/batches/{id}/results</c>: already a batch-lane call.</summary>
    Batch,

    /// <summary>OpenAI <c>/v1/files/{id}/content</c>: any file download takes this path, so
    /// only the lines found in it say the call was Batch output.</summary>
    File,
}
