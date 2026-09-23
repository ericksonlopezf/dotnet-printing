// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing;

/// <summary>Provides standard error code constants produced by the printing infrastructure.</summary>
public static class PrintingErrorCodes
{
    /// <summary>Represents an error code indicating a raw network socket transmission failure.</summary>
    public const string TransmissionError = "Printer.TransmissionError";

    /// <summary>Represents an error code indicating a socket connection failure.</summary>
    public const string SocketError = "Printer.SocketError";

    /// <summary>Represents an error code indicating that a printer operation timed out.</summary>
    public const string Timeout = "Printer.Timeout";

    /// <summary>Represents an error code indicating that a printer operation was canceled by the caller.</summary>
    public const string Canceled = "Printer.Canceled";
}
