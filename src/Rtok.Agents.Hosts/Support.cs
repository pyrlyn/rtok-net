namespace Rtok.Agents.Hosts;

/// <summary>A module's support on a host app, with the flag that writes it or why it cannot be written.</summary>
/// <param name="Level">The level.</param>
/// <param name="Note">The flag for <see cref="SupportLevel.Flag"/> and <see cref="SupportLevel.Offer"/>; the reason for <see cref="SupportLevel.No"/>.</param>
public readonly record struct Support(SupportLevel Level, string? Note = null)
{
    /// <summary>Written by a plain install.</summary>
    public static Support Yes { get; } = new(SupportLevel.Yes);

    /// <summary>Written only with <paramref name="flag"/>.</summary>
    public static Support Flag(string flag) => new(SupportLevel.Flag, flag);

    /// <summary>Offered behind <paramref name="flag"/>; the host's installer owns it.</summary>
    public static Support Offer(string flag) => new(SupportLevel.Offer, flag);

    /// <summary>Cannot be written, because <paramref name="reason"/>.</summary>
    public static Support No(string reason) => new(SupportLevel.No, reason);

    /// <summary>Whether install can write the module at all.</summary>
    public bool Writable => Level is SupportLevel.Yes or SupportLevel.Flag;
}
