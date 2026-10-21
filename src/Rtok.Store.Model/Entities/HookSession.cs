namespace Rtok.Store.Model;

/// <summary>Table <c>hook_sessions</c>: the session fields every hook stdin repeats, saved once per value set.</summary>
public class HookSession
{
    public long Id { get; set; }
    public string Fields { get; set; } = "";
}
