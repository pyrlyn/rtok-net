namespace Rtok.Agent.Sdk;

/// <summary>The one prompt shape every installer question uses, so each surface asks the same way.</summary>
public static class Prompts
{
    /// <summary>The question line, <c>? &lt;question&gt; [Y/n] </c> or <c>[y/N]</c>: plain text, no raw mode, nothing to erase typed input.</summary>
    public static string Line(string question, bool defaultYes) =>
        $"? {question} {(defaultYes ? "[Y/n]" : "[y/N]")} ";

    /// <summary>
    /// The answer in <paramref name="line"/>: Enter takes the default, <c>y</c> or <c>yes</c>
    /// accepts, anything else declines. <see langword="null"/> (end of input, Esc) is a no, so
    /// an unanswered question never acts.
    /// </summary>
    public static bool Answer(string? line, bool defaultYes)
    {
        if (line is null)
        {
            return false;
        }
        var a = line.Trim();
        return a.Length == 0
            ? defaultYes
            : a.Equals("y", StringComparison.OrdinalIgnoreCase)
                || a.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
