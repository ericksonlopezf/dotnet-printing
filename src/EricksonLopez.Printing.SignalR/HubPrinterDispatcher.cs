// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Dispatches print jobs to connected edge printer agents via SignalR groups.
/// </summary>
public sealed class HubPrinterDispatcher : IHubPrinterDispatcher
{
    private readonly IHubContext<PrinterHub, IPrinterHubClient> _hubContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HubPrinterDispatcher>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubPrinterDispatcher"/> class with the specified hub context and optional time provider.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context</param>
    /// <param name="timeProvider">The optional time provider used for timestamping</param>
    /// <exception cref="ArgumentNullException"><paramref name="hubContext"/> is <see langword="null"/></exception>
    public HubPrinterDispatcher(
        IHubContext<PrinterHub, IPrinterHubClient> hubContext,
        TimeProvider? timeProvider = null)
        : this(hubContext, timeProvider, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HubPrinterDispatcher"/> class with the specified hub context, time provider, and logger.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context</param>
    /// <param name="timeProvider">The optional time provider used for timestamping</param>
    /// <param name="logger">The optional logger for telemetry and diagnostics</param>
    /// <exception cref="ArgumentNullException"><paramref name="hubContext"/> is <see langword="null"/></exception>
    public HubPrinterDispatcher(
        IHubContext<PrinterHub, IPrinterHubClient> hubContext,
        TimeProvider? timeProvider,
        ILogger<HubPrinterDispatcher>? logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/></exception>
    /// <exception cref="OperationCanceledException">The operation was canceled</exception>
    public async Task<Result<bool>> DispatchAsync(
        string printerName,
        IPrintDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(printerName))
        {
            _logger?.LogWarning("Dispatch rejected: printer name cannot be null or empty.");
            return Error.Validation("Printer.InvalidName", "Printer name cannot be null or empty.");
        }

        var payloadBytes = document.GetBytes();
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        var message = new PrintJobMessage(
            document.IdempotencyKey ?? Guid.NewGuid().ToString("N"),
            printerName,
            document.DocumentName,
            payloadBase64,
            _timeProvider.GetUtcNow());

        var groupName = PrinterHub.GetPrinterGroupName(printerName);

        _logger?.LogInformation(
            SignalRPrintingEventIds.DispatchStarted,
            "Dispatching print job {JobId} ('{DocumentName}', {ByteCount} bytes) to SignalR group '{GroupName}'",
            message.JobId, document.DocumentName, payloadBytes.Length, groupName);

        try
        {
            await _hubContext.Clients.Group(groupName).OnPrintJobReceived(message).ConfigureAwait(false);
            _logger?.LogInformation(
                SignalRPrintingEventIds.DispatchSuccess,
                "Successfully dispatched print job {JobId} to SignalR group '{GroupName}'", message.JobId, groupName);
            return Result<bool>.Success(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(
                SignalRPrintingEventIds.DispatchFailed,
                ex, "Failed to dispatch print job {JobId} to SignalR group '{GroupName}': {Message}",
                message.JobId, groupName, ex.Message);
            return Error.Unavailable(
                "Printer.DispatchError",
                $"Failed to dispatch print job to SignalR group '{groupName}': {ex.Message}");
        }
    }
}
