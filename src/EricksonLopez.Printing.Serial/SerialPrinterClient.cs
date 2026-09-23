// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing.Serial;

/// <summary>
/// Provides a printer client implementation that transmits raw byte streams to POS receipt and thermal printers over serial COM, RS-232, or virtual COM ports.
/// </summary>
public sealed class SerialPrinterClient : IPrinterClient, IDisposable
{
    private readonly SerialPrinterClientOptions _options;
    private readonly ILogger<SerialPrinterClient>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPrinterClient"/> class with the specified options.
    /// </summary>
    /// <param name="options">The serial connection configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public SerialPrinterClient(SerialPrinterClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPrinterClient"/> class with the specified options and optional logger.
    /// </summary>
    /// <param name="options">The serial connection configuration options.</param>
    /// <param name="logger">The optional logger for telemetry and diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public SerialPrinterClient(SerialPrinterClientOptions options, ILogger<SerialPrinterClient>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The client has been disposed</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/></exception>
    public async Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (document.IsEmpty)
            {
                _logger?.LogDebug(
                    PrintingEventIds.SerialPrintSuccess,
                    "Empty print document '{DocumentName}' skipped.",
                    document.DocumentName);
                return Result<bool>.Success(true);
            }

            _logger?.LogInformation(
                PrintingEventIds.SerialPrintStarted,
                "Initiating print job '{DocumentName}' to serial printer at port {PortName} (Baud: {BaudRate})",
                document.DocumentName, _options.PortName, _options.BaudRate);

            try
            {
                using var serialPort = new SerialPort(
                    _options.PortName,
                    _options.BaudRate,
                    _options.Parity,
                    _options.DataBits,
                    _options.StopBits)
                {
                    Handshake = _options.Handshake,
                    WriteTimeout = _options.WriteTimeoutMs,
                    ReadTimeout = _options.ReadTimeoutMs
                };

                serialPort.Open();

                await document.WriteToAsync(serialPort.BaseStream, cancellationToken).ConfigureAwait(false);
                await serialPort.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                _logger?.LogInformation(
                    PrintingEventIds.SerialPrintSuccess,
                    "Successfully completed print job '{DocumentName}' to serial printer at port {PortName}",
                    document.DocumentName, _options.PortName);

                return Result<bool>.Success(true);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogError(
                    PrintingEventIds.SerialPrintFailed,
                    ex, "Access denied to serial port '{PortName}': {Message}", _options.PortName, ex.Message);
                return Error.Unavailable(
                    "Printer.SerialAccessDenied",
                    $"Access denied to serial port '{_options.PortName}': {ex.Message}");
            }
            catch (IOException ex)
            {
                _logger?.LogError(
                    PrintingEventIds.SerialPrintFailed,
                    ex, "Serial I/O error communicating with printer on '{PortName}': {Message}", _options.PortName, ex.Message);
                return Error.Unavailable(
                    "Printer.SerialIoError",
                    $"I/O error communicating with printer on '{_options.PortName}': {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                _logger?.LogWarning(
                    PrintingEventIds.SerialPrintFailed,
                    ex, "Timeout communicating with serial printer on '{PortName}': {Message}", _options.PortName, ex.Message);
                return Error.Unavailable(
                    "Printer.SerialTimeout",
                    $"Timeout communicating with printer on '{_options.PortName}': {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                _logger?.LogError(
                    PrintingEventIds.SerialPrintFailed,
                    ex, "Invalid serial port parameter '{PortName}': {Message}", _options.PortName, ex.Message);
                return Error.Validation(
                    "Printer.InvalidPortName",
                    $"Invalid serial port parameter '{_options.PortName}': {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogError(
                    PrintingEventIds.SerialPrintFailed,
                    ex, "Serial port invalid state on '{PortName}': {Message}", _options.PortName, ex.Message);
                return Error.Unavailable(
                    "Printer.SerialInvalidState",
                    $"Serial port '{_options.PortName}' is in an invalid state or already open: {ex.Message}");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // _gate.Dispose(); omitted intentionally to prevent ODE for concurrent waiters
    }
}
