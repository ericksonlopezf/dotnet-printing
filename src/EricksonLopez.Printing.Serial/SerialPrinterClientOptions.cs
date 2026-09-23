// Copyright © Erickson Lopez. MIT License.
using System.IO.Ports;

namespace EricksonLopez.Printing;

/// <summary>
/// Specifies configuration options for communicating with a POS receipt or label printer over a serial COM or RS-232 port.
/// </summary>
public sealed class SerialPrinterClientOptions
{
    /// <summary>
    /// Gets or sets the serial port name (e.g. "COM1", "COM3", or "/dev/ttyUSB0"). Default is "COM1".
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// Gets or sets the serial baud rate. Typical values are 9600, 19200, 38400, or 115200. Default is 9600.
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Gets or sets the parity-checking protocol. Default is <see cref="Parity.None"/>.
    /// </summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>
    /// Gets or sets the standard length of data bits per byte. Default is 8.
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Gets or sets the standard number of stop bits per byte. Default is <see cref="StopBits.One"/>.
    /// </summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>
    /// Gets or sets the handshaking protocol for serial port transmission. Default is <see cref="Handshake.None"/>.
    /// </summary>
    public Handshake Handshake { get; set; } = Handshake.None;

    /// <summary>
    /// Gets or sets the write timeout in milliseconds. Default is 5000 ms.
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the read timeout in milliseconds. Default is 2000 ms.
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 2000;
}
