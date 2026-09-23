// Copyright © Erickson Lopez. MIT License.
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing;

/// <summary>
/// Provides pre-allocated logging event identifiers for structured telemetry.
/// </summary>
/// <remarks>
/// Event ID ranges:
/// <list type="bullet">
///   <item><description>1001–1003: TCP printer lifecycle events.</description></item>
///   <item><description>1004–1006: Serial printer lifecycle events.</description></item>
///   <item><description>1010–1011: Resilient retry decorator events.</description></item>
/// </list>
/// </remarks>
public static class PrintingEventIds
{
    /// <summary>Represents the event identifier emitted when a TCP print job begins.</summary>
    public static readonly EventId TcpPrintStarted = new(1001, nameof(TcpPrintStarted));

    /// <summary>Represents the event identifier emitted when a TCP print job succeeds.</summary>
    public static readonly EventId TcpPrintSuccess = new(1002, nameof(TcpPrintSuccess));

    /// <summary>Represents the event identifier emitted when a TCP print job fails.</summary>
    public static readonly EventId TcpPrintFailed = new(1003, nameof(TcpPrintFailed));

    /// <summary>Represents the event identifier emitted when a serial print job begins.</summary>
    public static readonly EventId SerialPrintStarted = new(1004, nameof(SerialPrintStarted));

    /// <summary>Represents the event identifier emitted when a serial print job succeeds.</summary>
    public static readonly EventId SerialPrintSuccess = new(1005, nameof(SerialPrintSuccess));

    /// <summary>Represents the event identifier emitted when a serial print job fails.</summary>
    public static readonly EventId SerialPrintFailed = new(1006, nameof(SerialPrintFailed));

    /// <summary>Represents the event identifier emitted on a resilient retry attempt.</summary>
    public static readonly EventId RetryAttempt = new(1010, nameof(RetryAttempt));

    /// <summary>Represents the event identifier emitted when all retries have been exhausted.</summary>
    public static readonly EventId RetryExhausted = new(1011, nameof(RetryExhausted));
}
