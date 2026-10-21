namespace Rtok.Process;

/// <summary>Which of the child's streams a line came from.</summary>
public enum ChildOutput
{
    /// <summary>Standard output.</summary>
    Stdout,

    /// <summary>Standard error.</summary>
    Stderr,
}
