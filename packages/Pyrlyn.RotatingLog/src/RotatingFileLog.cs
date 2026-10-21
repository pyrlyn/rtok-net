using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Pyrlyn.RotatingLog;

/// <summary>
/// A rotating text log for short processes: one file, a level floor, <c>.N</c> rotation and an
/// <c>errors.log</c> copy, safe when several processes append at once. No host, no DI.
/// </summary>
/// <remarks>
/// A line is <c>2026-09-09 15:04:05 info source/name: message</c> (UTC). Every append runs under
/// an exclusive lock shared by all processes writing the same path: the size is checked, the
/// siblings are shifted when the line would pass <see cref="RotatingFileLogOptions.MaxBytes"/>,
/// and the line is appended whole. The lock is a file in the temp directory, never the log
/// itself, so a rename never meets an open handle of ours.
/// </remarks>
public sealed class RotatingFileLog
{
    private static readonly string[] Levels = ["error", "warn", "info", "debug"];

    private readonly RotatingFileLogOptions _options;
    private readonly int _floor;
    private readonly string _path;
    private readonly string? _errorsPath;

    /// <summary>Creates a log over <paramref name="options"/>. Nothing is touched on disk.</summary>
    /// <param name="options">The file, its bounds and the level floor.</param>
    public RotatingFileLog(RotatingFileLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.Path, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegative(options.Files, nameof(options));
        _options = options;
        _floor = Rank(options.Level);
        _path = System.IO.Path.GetFullPath(options.Path);
        _errorsPath = options.CopyErrors ? ErrorsPathFor(_path) : null;
    }

    /// <summary>The live file, as a full path.</summary>
    public string Path => _path;

    /// <summary>The errors copy, or null when <see cref="RotatingFileLogOptions.CopyErrors"/> is off.</summary>
    public string? ErrorsPath => _errorsPath;

    /// <summary>Whether the floor lets <paramref name="level"/> through. No IO.</summary>
    /// <param name="level">The line's level.</param>
    /// <returns>True when a line at this level would be written.</returns>
    public bool IsEnabled(string level) => Rank(level) <= _floor;

    /// <summary>
    /// Appends one line when the floor allows it, and copies an <c>error</c> line to
    /// <see cref="ErrorsPath"/>. Never throws: a log that cannot be written is not something the
    /// caller can act on.
    /// </summary>
    /// <param name="level"><c>error</c>, <c>warn</c>, <c>info</c> or <c>debug</c>; an unknown name ranks as <c>error</c>.</param>
    /// <param name="source">What wrote the line, for example a plugin id.</param>
    /// <param name="name">The event name.</param>
    /// <param name="message">The message; newlines become spaces.</param>
    public void Write(string level, string source, string name, string message)
    {
        var enabled = IsEnabled(level);
        var error =
            _errorsPath is not null
            && string.Equals(level, "error", StringComparison.OrdinalIgnoreCase);
        if (!enabled && !error)
        {
            return;
        }
        var text = FormatLine(_options.Time.GetUtcNow(), level, source, name, message);
        if (enabled)
        {
            TryAppend(_path, text);
        }
        if (error)
        {
            TryAppend(_errorsPath!, text);
        }
    }

    /// <summary>Whether <paramref name="floor"/> lets <paramref name="level"/> through. Unknown names rank as <c>error</c>.</summary>
    /// <param name="floor">The floor.</param>
    /// <param name="level">The line's level.</param>
    /// <returns>True when the line passes.</returns>
    public static bool IsEnabled(string floor, string level) => Rank(level) <= Rank(floor);

    /// <summary>One line, without the newline: <c>&lt;yyyy-MM-dd HH:mm:ss&gt; &lt;level&gt; &lt;source&gt;/&lt;name&gt;: &lt;message&gt;</c> in UTC.</summary>
    /// <param name="time">The time; written in UTC.</param>
    /// <param name="level">The level, as given.</param>
    /// <param name="source">What wrote the line.</param>
    /// <param name="name">The event name.</param>
    /// <param name="message">The message; <c>\n</c> and <c>\r</c> become spaces so one event stays one line.</param>
    /// <returns>The line.</returns>
    public static string FormatLine(
        DateTimeOffset time,
        string level,
        string source,
        string name,
        string message
    )
    {
        var flat = message.Replace('\n', ' ').Replace('\r', ' ');
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{time.UtcDateTime:yyyy-MM-dd HH:mm:ss} {level} {source}/{name}: {flat}"
        );
    }

    /// <summary><c>path</c> → <c>path.i</c>. The suffix follows the extension, so the live name stays stable.</summary>
    /// <param name="path">The live file.</param>
    /// <param name="i">The generation, 1 for the newest rotated sibling.</param>
    /// <returns>The sibling's path.</returns>
    public static string RotatedPath(string path, int i) =>
        string.Create(CultureInfo.InvariantCulture, $"{path}.{i}");

    /// <summary><c>errors.log</c> beside <paramref name="path"/>.</summary>
    /// <param name="path">The live file.</param>
    /// <returns>The errors copy's path.</returns>
    public static string ErrorsPathFor(string path) =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? "", "errors.log");

    private void TryAppend(string path, string text)
    {
        try
        {
            Append(path, text);
        }
        catch (Exception e)
            when (e is IOException or UnauthorizedAccessException or TimeoutException)
        {
            // Fail open: the caller cannot act on a log that cannot be written.
        }
    }

    /// <summary>Appends <paramref name="text"/> as its own line under the path's lock, rotating first when it would pass the bound.</summary>
    internal void Append(string path, string text)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var bytes = Encoding.UTF8.GetBytes(text + "\n");
        using var held = AcquireLock(path, _options.LockTimeout);
        if (Over(path, bytes.Length))
        {
            Rotate(path, _options.Files);
        }
        using var file = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete
        );
        file.Write(bytes);
    }

    /// <summary>Rotates under the lock when the live file would pass the bound: a size seen before the lock does not count.</summary>
    internal void RotateIfOver(string path, long incoming)
    {
        using var held = AcquireLock(path, _options.LockTimeout);
        if (Over(path, incoming))
        {
            Rotate(path, _options.Files);
        }
    }

    private bool Over(string path, long incoming)
    {
        var info = new FileInfo(path);
        var len = info.Exists ? info.Length : 0;
        return len > 0 && len + incoming > _options.MaxBytes;
    }

    private static void Rotate(string path, int files)
    {
        if (files == 0)
        {
            File.WriteAllBytes(path, []);
            return;
        }
        File.Delete(RotatedPath(path, files));
        for (var i = files - 1; i >= 1; i--)
        {
            var from = RotatedPath(path, i);
            if (File.Exists(from))
            {
                File.Move(from, RotatedPath(path, i + 1), overwrite: true);
            }
        }
        File.Move(path, RotatedPath(path, 1), overwrite: true);
    }

    /// <summary>
    /// An exclusive open of a temp-dir file named after the log's full path. On Unix .NET takes a
    /// non-blocking <c>flock</c> for <see cref="FileShare.None"/>; on Windows the share mode is the
    /// lock. Either way it is released when the process dies, so a crashed writer cannot wedge it.
    /// </summary>
    private static FileStream AcquireLock(string path, TimeSpan timeout)
    {
        var lockPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"pyrlyn-log-{Fnv1a(path):x16}.lock"
        );
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
            }
            catch (IOException) when (clock.Elapsed < timeout)
            {
                Thread.Sleep(1);
            }
            catch (IOException e)
            {
                throw new TimeoutException($"{path} stayed locked for {timeout}", e);
            }
        }
    }

    /// <summary>A hash that is the same in every process (string.GetHashCode is randomized).</summary>
    private static ulong Fnv1a(string text)
    {
        var hash = 14_695_981_039_346_656_037UL;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 1_099_511_628_211UL;
        }
        return hash;
    }

    private static int Rank(string level)
    {
        for (var i = 0; i < Levels.Length; i++)
        {
            if (string.Equals(Levels[i], level, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }
}
