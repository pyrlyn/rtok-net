using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rtok.Tasks;

/// <summary>
/// A task id: a prefix of 1 to 8 ASCII letters and a dotted path of 1 or 2 numbers from 1,
/// <c>R12</c> or <c>R2.1</c>. Input is case-insensitive, output upper-case. Ids order by
/// prefix, then numerically, so <c>R2 &lt; R2.1 &lt; R10</c>.
/// </summary>
[JsonConverter(typeof(TaskIdJsonConverter))]
public sealed class TaskId : IEquatable<TaskId>, IComparable<TaskId>
{
    /// <summary>The deepest path: a task and its subtasks.</summary>
    public const int MaxDepth = 2;

    /// <summary>The longest prefix.</summary>
    public const int MaxPrefix = 8;

    private readonly uint[] _path;

    private TaskId(string prefix, uint[] path)
    {
        Prefix = prefix;
        _path = path;
    }

    /// <summary>The upper-case prefix.</summary>
    public string Prefix { get; }

    /// <summary>The numbers, each from 1.</summary>
    public IReadOnlyList<uint> Path => _path;

    /// <summary>How many numbers: 1 for a task, 2 for a subtask.</summary>
    public int Depth => _path.Length;

    /// <summary>The id of <paramref name="prefix"/> and <paramref name="path"/>.</summary>
    /// <param name="prefix">1 to 8 ASCII letters, any case.</param>
    /// <param name="path">1 to 2 numbers from 1.</param>
    /// <returns>The id.</returns>
    /// <exception cref="TaskException">The prefix or the path is out of range.</exception>
    public static TaskId Of(string prefix, params uint[] path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var checkedPrefix = CheckPrefix(prefix);
        CheckPath(path);
        return new TaskId(checkedPrefix, [.. path]);
    }

    /// <summary>Parses <c>R12</c>, <c>r2.1</c> or <c> at7 </c>.</summary>
    /// <param name="text">The id as typed.</param>
    /// <returns>The id.</returns>
    /// <exception cref="TaskException">The text is not an id; the message says why.</exception>
    public static TaskId Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var s = text.Trim();
        var letters = 0;
        while (letters < s.Length && char.IsAsciiLetter(s[letters]))
        {
            letters++;
        }
        try
        {
            var rest = s[letters..];
            if (rest.Length == 0)
            {
                throw new TaskException($"task id \"{s}\" has no number");
            }
            var parts = rest.Split('.');
            var path = new uint[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                path[i] = Number(s, parts[i]);
            }
            return Of(s[..letters], path);
        }
        catch (TaskException e) when (!e.Message.StartsWith("task id \"", StringComparison.Ordinal))
        {
            throw new TaskException($"task id \"{s}\": {e.Message}", e);
        }
    }

    /// <summary>Parses an id, or returns false.</summary>
    /// <param name="text">The id as typed.</param>
    /// <param name="id">The id when the text is one.</param>
    /// <returns>Whether the text is an id.</returns>
    public static bool TryParse(
        string? text,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TaskId? id
    )
    {
        id = null;
        if (text is null)
        {
            return false;
        }
        try
        {
            id = Parse(text);
            return true;
        }
        catch (TaskException)
        {
            return false;
        }
    }

    /// <summary>Checks a prefix and returns it upper-case.</summary>
    /// <param name="prefix">The prefix as typed.</param>
    /// <returns>The upper-case prefix.</returns>
    /// <exception cref="TaskException">It is not 1 to 8 ASCII letters.</exception>
    public static string CheckPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length is 0 or > MaxPrefix || !prefix.All(char.IsAsciiLetter))
        {
            throw new TaskException(
                $"task prefix \"{prefix}\" must be 1 to {MaxPrefix} ASCII letters"
            );
        }
        return prefix.ToUpperInvariant();
    }

    /// <summary>The parent: <c>R2</c> for <c>R2.1</c>, null for a top-level task.</summary>
    public TaskId? Parent => Depth == 1 ? null : new TaskId(Prefix, _path[..^1]);

    /// <summary>The subtask numbered <paramref name="number"/>.</summary>
    /// <param name="number">The number from 1.</param>
    /// <returns>The subtask's id.</returns>
    /// <exception cref="TaskException">This id is already as deep as ids go.</exception>
    public TaskId Child(uint number)
    {
        uint[] path = [.. _path, number];
        CheckPath(path);
        return new TaskId(Prefix, path);
    }

    /// <summary>The path without the prefix: <c>2.1</c> for <c>R2.1</c>.</summary>
    public string Numbers => string.Join('.', _path);

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder(Prefix);
        for (var i = 0; i < _path.Length; i++)
        {
            if (i > 0)
            {
                sb.Append('.');
            }
            sb.Append(_path[i].ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <inheritdoc/>
    public bool Equals(TaskId? other) =>
        other is not null
        && string.Equals(Prefix, other.Prefix, StringComparison.Ordinal)
        && _path.AsSpan().SequenceEqual(other._path);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TaskId);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Prefix, StringComparer.Ordinal);
        foreach (var n in _path)
        {
            hash.Add(n);
        }
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public int CompareTo(TaskId? other)
    {
        if (other is null)
        {
            return 1;
        }
        var byPrefix = string.CompareOrdinal(Prefix, other.Prefix);
        return byPrefix != 0 ? byPrefix : _path.AsSpan().SequenceCompareTo(other._path);
    }

    /// <summary>Equality by value.</summary>
    public static bool operator ==(TaskId? a, TaskId? b) => a?.Equals(b) ?? b is null;

    /// <summary>Inequality by value.</summary>
    public static bool operator !=(TaskId? a, TaskId? b) => !(a == b);

    /// <summary>Order by prefix, then numerically.</summary>
    public static bool operator <(TaskId a, TaskId b) => Compare(a, b) < 0;

    /// <summary>Order by prefix, then numerically.</summary>
    public static bool operator >(TaskId a, TaskId b) => Compare(a, b) > 0;

    /// <summary>Order by prefix, then numerically.</summary>
    public static bool operator <=(TaskId a, TaskId b) => Compare(a, b) <= 0;

    /// <summary>Order by prefix, then numerically.</summary>
    public static bool operator >=(TaskId a, TaskId b) => Compare(a, b) >= 0;

    private static int Compare(TaskId a, TaskId b)
    {
        ArgumentNullException.ThrowIfNull(a);
        return a.CompareTo(b);
    }

    private static uint Number(string id, string part)
    {
        // Digits only, no sign, no leading zero; zero itself is refused by CheckPath.
        if (
            part.Length == 0
            || !part.All(char.IsAsciiDigit)
            || (part.Length > 1 && part[0] == '0')
            || !uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
        )
        {
            throw new TaskException($"task id \"{id}\": \"{part}\" is not a number from 1");
        }
        return n;
    }

    private static void CheckPath(uint[] path)
    {
        if (path.Length is 0 or > MaxDepth)
        {
            throw new TaskException($"task id needs 1 to {MaxDepth} numbers, got {path.Length}");
        }
        if (path.Contains(0u))
        {
            throw new TaskException("task numbers start at 1");
        }
    }
}
