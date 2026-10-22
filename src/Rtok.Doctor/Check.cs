namespace Rtok.Doctor;

/// <summary>A verdict and why.</summary>
internal readonly record struct Check(Verdict Verdict, string Detail);
