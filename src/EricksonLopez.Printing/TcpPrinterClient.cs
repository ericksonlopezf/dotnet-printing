// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing;

/// <summary>Transmits raw byte streams to a network socket printer over a TCP connection.</summary>
public sealed class TcpPrinterClient : IPrinterClient
{
    private readonly TcpPrinterClientOptions _options;
    private readonly ILogger<TcpPrinterClient>? _logger;

    internal TcpPrinterClientOptions Options => _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpPrinterClient"/> class with the specified options.
    /// </summary>
    /// <param name="options">The connection and socket configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public TcpPrinterClient(TcpPrinterClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpPrinterClient"/> class with the specified options and logger.
    /// </summary>
    /// <param name="options">The connection and socket configuration options.</param>
    /// <param name="logger">The optional logger for telemetry and diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public TcpPrinterClient(TcpPrinterClientOptions options, ILogger<TcpPrinterClient>? logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public async Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.IsEmpty)
        {
            _logger?.LogDebug("Empty print document '{DocumentName}' passed to TCP printer {Host}:{Port}; returning success immediately.",
                document.DocumentName, _options.Host, _options.Port);
            return Result<bool>.Success(true);
        }

        _logger?.LogInformation(
            PrintingEventIds.TcpPrintStarted,
            "Initiating print job '{DocumentName}' to TCP printer at {Host}:{Port}",
            document.DocumentName, _options.Host, _options.Port);

        try
        {
            using var client = CreateTcpClient();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.TimeoutMs);

            await client.ConnectAsync(_options.Host, _options.Port, cts.Token).ConfigureAwait(false);

            await using var stream = StreamFactory(client);
            await document.WriteToAsync(stream, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);

            _logger?.LogInformation(
                PrintingEventIds.TcpPrintSuccess,
                "Successfully completed print job '{DocumentName}' to TCP printer at {Host}:{Port}",
                document.DocumentName, _options.Host, _options.Port);

            return Result<bool>.Success(true);
        }
        catch (SocketException ex)
        {
            _logger?.LogError(
                PrintingEventIds.TcpPrintFailed,
                ex, "Socket error connecting to TCP printer at {Host}:{Port}: {Message}",
                _options.Host, _options.Port, ex.Message);
            return Error.Unavailable(
                "Printer.SocketError",
                $"Failed to connect to printer at '{_options.Host}:{_options.Port}': {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger?.LogError(
                PrintingEventIds.TcpPrintFailed,
                ex, "Network I/O transmission error printing to {Host}:{Port}: {Message}",
                _options.Host, _options.Port, ex.Message);
            return Error.Unavailable(
                "Printer.TransmissionError",
                $"Network IO error while sending print job to '{_options.Host}:{_options.Port}': {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Error.Unavailable(
                    PrintingErrorCodes.Canceled,
                    "Print operation was canceled by the caller.");
            }

            _logger?.LogWarning(
                PrintingEventIds.TcpPrintFailed,
                ex, "Timed out after {TimeoutMs}ms waiting for TCP printer at {Host}:{Port}",
                _options.TimeoutMs, _options.Host, _options.Port);
            return Error.Unavailable(
                PrintingErrorCodes.Timeout,
                $"Timed out after {_options.TimeoutMs}ms trying to print to '{_options.Host}:{_options.Port}'.");
        }
    }

    internal Func<TcpClient, Stream> StreamFactory { get; set; } = static c => c.GetStream();

    internal TcpClient CreateTcpClient()
    {
        var client = new TcpClient();
        client.NoDelay = _options.NoDelay;
        if (_options.LingerSeconds >= 0)
        {
            client.LingerState = new LingerOption(true, _options.LingerSeconds);
        }

        return client;
    }
}

