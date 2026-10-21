using Pyrlyn.FsKit;
using Pyrlyn.RotatingLog;

namespace Rtok.Log;

/// <summary>
/// Reads the live log as it grows, across rotations. It keeps the file it reads open and knows
/// it by device and inode, not by its lines (two generations can start with the same line): when
/// the path names another file, the rest of the old one comes off the open handle, wherever the
/// rename left it, even after retention deleted it. The rotated siblings are history that
/// <see cref="LogTail"/> already showed.
/// </summary>
public sealed class LogFollow : IDisposable
{
    private Opened? _reading;
    private int _consumed;

    private LogFollow() { }

    /// <summary>
    /// A follow already caught up with <paramref name="path"/>, and the lines that file holds:
    /// one read feeds both, so a line written in between is new to both or old to both.
    /// </summary>
    /// <param name="path">The live log file; it may not exist yet.</param>
    /// <returns>The follow and the live file's lines.</returns>
    public static (LogFollow Follow, IReadOnlyList<string> Lines) Primed(string path)
    {
        var follow = new LogFollow();
        if (Opened.Open(path) is not { } opened)
        {
            return (follow, []);
        }
        follow._reading = opened;
        follow._consumed = opened.Lines.Count;
        return (follow, opened.Lines);
    }

    /// <summary>
    /// The lines written to <paramref name="path"/> since the last call, oldest first. A path
    /// renamed away and not yet recreated reads as nothing new; a file truncated in place is
    /// read again from its start.
    /// </summary>
    /// <param name="path">The live log file.</param>
    /// <returns>The new lines.</returns>
    public IReadOnlyList<string> Poll(string path)
    {
        if (Opened.Open(path) is not { } now)
        {
            return [];
        }
        if (_reading is { } reading && reading.Id == now.Id)
        {
            now.Dispose();
            var at = now.Lines.Count >= _consumed ? _consumed : 0;
            _consumed = now.Lines.Count;
            return now.Lines[at..];
        }

        // A rotation. First the rest of the file we were reading, then whole generations that
        // rotated past us between two polls (oldest first), then the newcomer from its start.
        var fresh = new List<string>();
        if (_reading is { } old)
        {
            var rest = old.Reread();
            fresh.AddRange(rest.Skip(Math.Min(_consumed, rest.Count)));
            var passed = new List<List<string>>();
            for (var i = 1; Opened.Open(RotatedPath(path, i)) is { } sibling; i++)
            {
                using (sibling)
                {
                    if (sibling.Id == old.Id)
                    {
                        break;
                    }
                    passed.Add(sibling.Lines);
                }
            }
            passed.Reverse();
            fresh.AddRange(passed.SelectMany(lines => lines));
            old.Dispose();
        }
        _reading = now;
        _consumed = now.Lines.Count;
        fresh.AddRange(now.Lines);
        return fresh;
    }

    /// <inheritdoc />
    public void Dispose() => _reading?.Dispose();

    private static string RotatedPath(string path, int i) => RotatingFileLog.RotatedPath(path, i);

    /// <summary>An open log file, which file it is, and the whole lines it held when opened.</summary>
    private sealed class Opened : IDisposable
    {
        private readonly FileStream _stream;

        private Opened(FileStream stream, FileId id, List<string> lines)
        {
            _stream = stream;
            Id = id;
            Lines = lines;
        }

        public FileId Id { get; }

        public List<string> Lines { get; }

        /// <summary>
        /// Opens <paramref name="path"/> for reading without blocking a writer's rename or delete.
        /// Its id is read before and after the open; when they differ the path changed under us
        /// and this is a delay, not a file.
        /// </summary>
        public static Opened? Open(string path)
        {
            if (!FsPath.TryGetFileId(path, out var before))
            {
                return null;
            }
            FileStream stream;
            try
            {
                stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
            if (!FsPath.TryGetFileId(path, out var after) || after != before)
            {
                stream.Dispose();
                return null;
            }
            var opened = new Opened(stream, before, []);
            opened.Lines.AddRange(opened.Reread());
            return opened;
        }

        /// <summary>The file's whole lines now, read through the open handle.</summary>
        public List<string> Reread()
        {
            try
            {
                _stream.Position = 0;
                using var buffer = new MemoryStream();
                _stream.CopyTo(buffer);
                return LogTail.WholeLines(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }

        public void Dispose() => _stream.Dispose();
    }
}
