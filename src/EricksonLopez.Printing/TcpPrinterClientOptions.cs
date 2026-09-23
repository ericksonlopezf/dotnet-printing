// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing;

/// <summary>
/// Specifies configuration options for connecting to a network printer over raw TCP sockets.
/// </summary>
public sealed class TcpPrinterClientOptions
{
    /// <summary>
    /// Gets or sets the target printer IP address or hostname. Defaults to "127.0.0.1".
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the raw socket port number. Defaults to 9100.
    /// </summary>
    public int Port { get; set; } = 9100;

    /// <summary>
    /// Gets or sets the socket connect and write timeout in milliseconds. Defaults to 5000 milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets a value indicating whether to disable Nagle's algorithm (TCP_NODELAY) to minimize transmission latency. Defaults to <see langword="true"/>.
    /// </summary>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the socket linger time in seconds to control socket closure behavior. Defaults to 0.
    /// </summary>
    public int LingerSeconds { get; set; }
}
