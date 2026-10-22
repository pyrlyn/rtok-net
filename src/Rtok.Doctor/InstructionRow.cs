using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>One instruction source.</summary>
/// <param name="Name"><c>claude-user</c>, <c>claude-project</c>, <c>agents-project</c>, <c>plugin:…</c>, an injector.</param>
/// <param name="Tokens">Its estimated tokens.</param>
/// <param name="Path">The file, or <c>mcp:&lt;name&gt;</c> for an injector without one.</param>
/// <param name="Warn">Over <c>[doctor] instruction_warn_tokens</c>.</param>
public sealed record InstructionRow(string Name, int Tokens, string Path, bool Warn);
