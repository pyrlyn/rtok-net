namespace Rtok.Proxy;

/// <summary>The <c>SSL_CERT_FILE</c> bundle cannot be used; the proxy does not start.</summary>
public sealed class ProxyTlsException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ProxyTlsException() { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, with the path.</param>
    public ProxyTlsException(string message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, with the path.</param>
    /// <param name="inner">The cause.</param>
    public ProxyTlsException(string message, Exception inner)
        : base(message, inner) { }
}
