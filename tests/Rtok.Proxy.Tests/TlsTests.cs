using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Proxy.Tests;

/// <summary><c>SSL_CERT_FILE</c>: a corporate root the platform store lacks is trusted, and a
/// bundle that cannot be used stops the start instead of trusting less.</summary>
public sealed class TlsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_upstream_signed_by_the_bundle_root_is_trusted()
    {
        using var dir = new TempDir("rtok-tls-");
        using var ca = Authority();
        using var leaf = Leaf(ca);
        var bundle = dir.Write("roots.pem", ca.ExportCertificatePem());
        await using var upstream = await HttpsAsync(leaf);
        var config = new RtokConfig();
        config.Proxy.Port = 0;
        config.Proxy.Enabled = false;
        config.Proxy.Upstream = upstream.Urls.Single();

        await using (
            var trusted = await ProxyServer.StartAsync(
                new ProxyOptions(config) { CertFile = bundle },
                Ct
            )
        )
        {
            using var client = new HttpClient();
            Assert.Equal("tls ok", await client.GetStringAsync(new Uri(trusted.Address, "/x"), Ct));
        }
        await using var platform = await ProxyServer.StartAsync(
            new ProxyOptions(config) { CertFile = null },
            Ct
        );
        using var plain = new HttpClient();
        Assert.Equal(
            HttpStatusCode.BadGateway,
            (await plain.GetAsync(new Uri(platform.Address, "/x"), Ct)).StatusCode
        );
    }

    [Theory]
    [InlineData("missing.pem", "cannot open")]
    [InlineData("garbage.pem", "cannot parse")]
    [InlineData("empty.pem", "no certificates in")]
    public async Task A_bundle_that_cannot_be_used_stops_the_start(string file, string message)
    {
        using var dir = new TempDir("rtok-tls-");
        dir.Write("garbage.pem", "-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n");
        dir.Write("empty.pem", "no pem here\n");
        var config = new RtokConfig();
        config.Proxy.Port = 0;

        var error = await Assert.ThrowsAsync<ProxyTlsException>(() =>
            ProxyServer.StartAsync(new ProxyOptions(config) { CertFile = dir.Combine(file) }, Ct)
        );

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    private static X509Certificate2 Authority()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=rtok test root", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true
            )
        );
        var now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
    }

    private static X509Certificate2 Leaf(X509Certificate2 ca)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false)
        );
        var now = DateTimeOffset.UtcNow;
        using var issued = request.Create(
            ca,
            now.AddHours(-1),
            now.AddHours(12),
            RandomNumberGenerator.GetBytes(8)
        );
        using var withKey = issued.CopyWithPrivateKey(key);
        // Kestrel on macOS wants a key it can persist, so the pair goes through PKCS#12.
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx), null);
    }

    private static async Task<WebApplication> HttpsAsync(X509Certificate2 certificate)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
            k.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate))
        );
        var app = builder.Build();
        app.Run(context => context.Response.WriteAsync("tls ok"));
        await app.StartAsync(Ct);
        return app;
    }
}
