namespace Rtok.Doctor;

/// <summary>What a hook command's check found.</summary>
internal enum Verdict
{
    Ok,
    Broken,
    Suspect,
    Unverified,
}
