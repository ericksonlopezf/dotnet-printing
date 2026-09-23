// Copyright © Erickson Lopez. MIT License.
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Provides standardized <see cref="EventId"/> constants for structured telemetry in the SignalR printing bridge.
/// </summary>
/// <remarks>
/// Event ID range 2001–2003 is reserved for SignalR dispatcher events to avoid collision
/// with core transport Event IDs (1001–1011).
/// </remarks>
public static class SignalRPrintingEventIds
{
    /// <summary>
    /// Event ID emitted when a print job dispatch to an edge printer is initiated over SignalR.
    /// </summary>
    public static readonly EventId DispatchStarted = new(2001, nameof(DispatchStarted));

    /// <summary>
    /// Event ID emitted when a print job is successfully acknowledged and dispatched to the edge client.
    /// </summary>
    public static readonly EventId DispatchSuccess = new(2002, nameof(DispatchSuccess));

    /// <summary>
    /// Event ID emitted when a print job dispatch to an edge printer fails.
    /// </summary>
    public static readonly EventId DispatchFailed = new(2003, nameof(DispatchFailed));
}
