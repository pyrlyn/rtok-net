namespace Rtok.Config;

/// <summary>chars per token per class, ±15 %; `rtok stats --calibrate` rewrites</summary>
/// <remarks>Read from [estimator].</remarks>
public sealed class EstimatorSection
{
    /// <summary>characters per token in code</summary>
    public double Code { get; set; } = 3.5;

    /// <summary>characters per token in prose</summary>
    public double Prose { get; set; } = 4.2;

    /// <summary>characters per token in JSON</summary>
    public double Json { get; set; } = 3.0;

    /// <summary>characters per token in CJK text</summary>
    public double Cjk { get; set; } = 1.0;
}
