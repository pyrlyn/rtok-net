namespace Rtok.Doctor;

/// <summary>
/// Where a hook or server entry comes from, in keep order: of two equal copies the lower one is
/// kept (managed &gt; plugin &gt; project shared &gt; user &gt; local).
/// </summary>
internal enum Scope
{
    /// <summary>An admin's managed settings file: read, never edited.</summary>
    Managed,

    /// <summary>A plugin's own file: never edited.</summary>
    Plugin,

    /// <summary>A project file checked into the repository.</summary>
    Project,

    /// <summary>The user's own file.</summary>
    User,

    /// <summary>A local override (<c>settings.local.json</c>, <c>~/.claude.json</c> projects).</summary>
    Local,
}
