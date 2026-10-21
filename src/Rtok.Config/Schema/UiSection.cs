namespace Rtok.Config;

/// <summary>rtok's own lines on a terminal; pipes, --json stay plain</summary>
/// <remarks>Read from [ui].</remarks>
public sealed class UiSection
{
    /// <summary>emoji before status/warn/error lines (RTOK_UI_EMOJI)</summary>
    public bool Emoji { get; set; } = true;

    /// <summary>colour them; NO_COLOR/CLICOLOR_FORCE apply (RTOK_UI_COLOR)</summary>
    public bool Color { get; set; } = true;
}
