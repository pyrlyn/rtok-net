namespace Rtok.Config;

/// <summary>provider Batch result files</summary>
/// <remarks>Read from [proxy.batch].</remarks>
public sealed class BatchPolicy
{
    /// <summary>read a fetched results file into one usage row per request; the body is forwarded as is</summary>
    public bool ParseResults { get; set; }
}
