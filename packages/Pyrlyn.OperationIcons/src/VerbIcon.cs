namespace Pyrlyn.OperationIcons;

/// <summary>One row of an icon table: a verb fragment and the icon of every verb that contains it.</summary>
/// <param name="Key">The lowercase fragment, for example <c>install</c> or <c>remov</c>.</param>
/// <param name="Icon">The icon: one code point that is wide by default.</param>
public readonly record struct VerbIcon(string Key, string Icon);
