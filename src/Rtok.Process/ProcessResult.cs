using System.Diagnostics;

namespace Rtok.Process;

/// <summary>What a finished run hands back.</summary>
/// <param name="ExitCode">The child's code; <c>128 + signal</c> for a Unix signal death.</param>
/// <param name="Stdout">What the child wrote to stdout, as far as the cap allowed; empty when not captured.</param>
/// <param name="Stderr">The same for stderr.</param>
/// <param name="Dropped">Bytes read past the cap and not kept.</param>
public sealed record ProcessResult(int ExitCode, byte[] Stdout, byte[] Stderr, long Dropped);
