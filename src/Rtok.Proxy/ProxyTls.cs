using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Rtok.Proxy;

/// <summary>The upstream client's TLS trust: the platform store, plus the PEM bundle
/// <c>SSL_CERT_FILE</c> names (curl's convention, rtok T53.3) for corporate CAs.</summary>
public static class ProxyTls
{
    /// <summary>The variable naming the extra bundle.</summary>
    public const string CertFileVariable = "SSL_CERT_FILE";

    /// <summary>The certificates in <paramref name="path"/>. Fails closed: a missing,
    /// unparseable or certificate-free bundle is an error naming the path.</summary>
    /// <param name="path">The PEM bundle.</param>
    /// <returns>The roots it adds.</returns>
    /// <exception cref="ProxyTlsException">The bundle is unusable.</exception>
    public static X509Certificate2Collection LoadBundle(string path)
    {
        var roots = new X509Certificate2Collection();
        try
        {
            roots.ImportFromPemFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ProxyTlsException($"{CertFileVariable}: cannot open {path}: {e.Message}", e);
        }
        catch (CryptographicException e)
        {
            throw new ProxyTlsException($"{CertFileVariable}: cannot parse {path}: {e.Message}", e);
        }
        if (roots.Count == 0)
        {
            throw new ProxyTlsException($"{CertFileVariable}: no certificates in {path}");
        }
        return roots;
    }

    /// <summary>Configures <paramref name="handler"/> to trust <paramref name="extra"/> beside the
    /// platform store. A certificate the platform accepts passes; one it rejects for its chain
    /// alone passes when it chains to an extra root. A name mismatch never passes.</summary>
    /// <param name="handler">The upstream handler.</param>
    /// <param name="extra">The bundle's roots.</param>
    public static void Trust(SocketsHttpHandler handler, X509Certificate2Collection extra)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(extra);
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
            errors == SslPolicyErrors.None
            || (
                errors == SslPolicyErrors.RemoteCertificateChainErrors
                && certificate is X509Certificate2 leaf
                && ChainsTo(leaf, chain, extra)
            );
    }

    private static bool ChainsTo(
        X509Certificate2 leaf,
        X509Chain? presented,
        X509Certificate2Collection extra
    )
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(extra);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (presented is not null)
        {
            foreach (var element in presented.ChainElements)
            {
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }
        return chain.Build(leaf);
    }
}
