using System.Security.Cryptography;
using System.Text;

namespace Rtok.Web;

/// <summary>The per-run secret <c>/ws</c> requires (rtok S2): a fresh random value each start,
/// handed to the operator in the printed address, Jupyter style.</summary>
public static class WebToken
{
    /// <summary>A new token: 32 random bytes, base64url without padding.</summary>
    /// <returns>The token.</returns>
    public static string New() =>
        Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>Whether <paramref name="presented"/> is <paramref name="expected"/>, compared in
    /// constant time.</summary>
    /// <param name="expected">This run's token.</param>
    /// <param name="presented">What the request carried; null when it carried none.</param>
    /// <returns>True on a match.</returns>
    public static bool Matches(string expected, string? presented) =>
        presented is not null
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(presented)
        );
}
