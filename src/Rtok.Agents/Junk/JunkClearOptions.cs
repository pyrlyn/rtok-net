using Pyrlyn.Trash;
using Rtok.Config;

namespace Rtok.Agents.Junk;

/// <summary>One <c>clear</c> run: the report it plans from and the seams it removes through.</summary>
public sealed record JunkClearOptions
{
    /// <summary>The loaded config.</summary>
    public required RtokConfig Config { get; init; }

    /// <summary>The <c>list</c> report: the plan is its own items, no second scanner.</summary>
    public required JunkReport Report { get; init; }

    /// <summary>The flags.</summary>
    public required JunkFilter Filter { get; init; }

    /// <summary>The clock and how reasons read.</summary>
    public required JunkView View { get; init; }

    /// <summary>The paths never removed nor emptied around, real: see <see cref="JunkClear.Protected"/>.</summary>
    public required IReadOnlyList<string> Protected { get; init; }

    /// <summary>The store: rtok's archives and their retention. Null when it cannot be opened.</summary>
    public IJunkStore? Store { get; init; }

    /// <summary>Whether a host runs now; asked only when an item depends on it.</summary>
    public Func<string, bool> Running { get; init; } = _ => false;

    /// <summary><c>--trash</c>'s move.</summary>
    public Action<string> Trash { get; init; } = p => OsTrash.Move(p);

    /// <summary>Removes a stale worktree through git and says how; null when nothing can.</summary>
    public Func<string, string>? RemoveWorktree { get; init; }

    /// <summary>Only these paths: the ones a user was shown and confirmed. Null takes the whole plan.</summary>
    public IReadOnlyCollection<string>? Only { get; init; }
}
