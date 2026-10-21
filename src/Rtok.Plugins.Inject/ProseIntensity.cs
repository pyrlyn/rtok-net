namespace Rtok.Plugins.Inject;

/// <summary>How hard <see cref="ProseCompressor.Compress"/> strips; the names follow the terse
/// mode's levels.</summary>
public enum ProseIntensity
{
    /// <summary>Only leading pleasantries (<c>Sure!</c>, <c>I'd be happy to help</c>).</summary>
    Lite,

    /// <summary>Lite, plus filler words and the articles <c>a</c>, <c>an</c>, <c>the</c>.</summary>
    Full,

    /// <summary>Full, plus conjunction padding (<c>and then</c>, <c>as well as</c>).</summary>
    Ultra,
}
