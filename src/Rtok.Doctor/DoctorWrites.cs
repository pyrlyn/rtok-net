using Pyrlyn.FsKit;
using Rtok.Agent.Sdk;

namespace Rtok.Doctor;

/// <summary>The real <see cref="FixWrite"/>s.</summary>
public static class DoctorWrites
{
    /// <summary>
    /// Backs the file up into <c>_backup/</c> beside it (one generation per distinct content,
    /// <paramref name="keep"/> kept, 0 keeps all), then writes it atomically through symlinks.
    /// </summary>
    /// <param name="backup">Take a backup first (<c>[setup] backup</c>).</param>
    /// <param name="keep">Generations to keep (<c>[setup] backup_files</c>).</param>
    /// <returns>The writer.</returns>
    public static FixWrite Disk(bool backup, int keep) =>
        (path, body) =>
        {
            string? bak = null;
            if (backup)
            {
                try
                {
                    bak = Backups.Take(path, keep);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    throw new IOException($"backup failed: {e.Message}", e);
                }
            }
            try
            {
                AtomicFile.Write(path, body);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"write failed: {e.Message}", e);
            }
            return bak;
        };
}
