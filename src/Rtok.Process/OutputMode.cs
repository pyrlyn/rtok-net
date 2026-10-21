namespace Rtok.Process;

/// <summary>Where the child's output goes.</summary>
public enum OutputMode
{
    /// <summary>Keep stdout and stderr; an indicator shows until the child exits.</summary>
    Capture,

    /// <summary>Forward both streams live, hiding the indicator around each write.</summary>
    Stream,

    /// <summary>The child owns the terminal; no indicator.</summary>
    Inherit,

    /// <summary>A background child with nothing on its stdio; no indicator. <see cref="ProgressRunner.Spawn"/> only.</summary>
    Detached,
}
