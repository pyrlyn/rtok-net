namespace Rtok.Process;

/// <summary>Reads a child's output line by line and reports the progress it shows.</summary>
public interface IProgressParser
{
    /// <summary>One line, split on <c>\n</c> and <c>\r</c>; called for both streams, never at once.</summary>
    /// <param name="stream">The stream the line came from.</param>
    /// <param name="line">The line, without its end.</param>
    /// <returns>The progress it shows, or null.</returns>
    ProgressUpdate? Feed(ChildOutput stream, string line);
}
