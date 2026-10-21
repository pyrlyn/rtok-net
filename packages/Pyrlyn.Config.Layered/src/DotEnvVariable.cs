namespace Pyrlyn.Config.Layered;

/// <summary>One <c>NAME=value</c> assignment from a <c>.env</c> file.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="Value">The value, unquoted and unescaped.</param>
/// <param name="File">The file.</param>
/// <param name="Line">The 1-based line the assignment starts on.</param>
public sealed record DotEnvVariable(string Name, string Value, string File, int Line);
