using System.Security.Cryptography;
using System.Text;

namespace Rtok.Core;

/// <summary>A per-run secret for an rtok HTTP surface (rtok S2, S5): a fresh random value each
/// start, handed to the operator, Jupyter style.</summary>
public static class AccessToken
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

    /// <summary>The token a request presented: <c>Authorization: Bearer &lt;token&gt;</c>, else
    /// <c>?token=</c> for a browser.</summary>
    /// <param name="authorization">The <c>Authorization</c> header.</param>
    /// <param name="query">The <c>token</c> query value.</param>
    /// <returns>The token, or null.</returns>
    public static string? Presented(string? authorization, string? query)
    {
        if (!string.IsNullOrEmpty(authorization))
        {
            const string Bearer = "Bearer ";
            return authorization.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)
                ? authorization[Bearer.Length..].Trim()
                : null;
        }
        return string.IsNullOrEmpty(query) ? null : query;
    }
}
