using Rtok.Agents.Hosts;
using Rtok.Config;

namespace Rtok.Doctor;

/// <summary>
/// Writes a fixed host file after backing it up; returns the backup, or null when none was
/// taken. Throws <see cref="IOException"/> with <c>backup failed: …</c> or <c>write failed: …</c>.
/// </summary>
/// <param name="path">The file.</param>
/// <param name="body">Its new text.</param>
public delegate string? FixWrite(string path, string body);
