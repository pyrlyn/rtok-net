namespace Rtok.Doctor;

/// <summary>How a hook file spells its hooks.</summary>
internal enum HookShape
{
    /// <summary><c>hooks.&lt;Event&gt;[group].hooks[i]</c>, or the flat <c>hooks.&lt;Event&gt;[i]</c>: Claude, Cursor, Gemini.</summary>
    Json,

    /// <summary>Kimi's <c>[[hooks]]</c> tables of <c>{event, matcher, command}</c>.</summary>
    Kimi,

    /// <summary>CodeWhale's <c>[[hooks.hooks]]</c> tables.</summary>
    CodeWhale,

    /// <summary>Codex's <c>[[hooks.&lt;Event&gt;]]</c> groups: the JSON shape in TOML.</summary>
    Codex,
}
