namespace Rtok.Agents.Usage;

/// <summary>Reading one session file: an unreadable one is skipped, never fatal (fail open).</summary>
internal static class LogText
{
    /// <summary>The file's text, or null when it cannot be read.</summary>
    public static string? Read(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
