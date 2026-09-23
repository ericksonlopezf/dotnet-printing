// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing;

/// <summary>
/// Provides high-throughput network socket printing using a persistent TCP connection.
/// </summary>
/// <remarks>
/// Access to the underlying socket is serialized using a <see cref="SemaphoreSlim"/> to prevent concurrent interleaved byte streams.
/// </remarks>
public sealed class PooledTcpPrinterClient : IPrinterClient, IAsyncDisposable, IDisposable
{
    private readonly TcpPrinterClientOptions _options;
    private readonly ILogger<PooledTcpPrinterClient>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledTcpPrinterClient"/> class with the specified options.
    /// </summary>
    /// <param name="options">The connection and socket configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public PooledTcpPrinterClient(TcpPrinterClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledTcpPrinterClient"/> class with the specified options and logger.
    /// </summary>
    /// <param name="options">The connection and socket configuration options.</param>
    /// <param name="logger">The optional logger for telemetry and diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public PooledTcpPrinterClient(TcpPrinterClientOptions options, ILogger<PooledTcpPrinterClient>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Pre-establishes the persistent TCP connection to the printer endpoint during application
    /// startup or container readiness probes, eliminating first-print TCP handshake latency.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the connection has been established.</returns>
    /// <exception cref="ObjectDisposedException">This client has already been disposed.</exception>
    /// <exception cref="SocketException">The connection to the printer endpoint could not be established.</exception>
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.TimeoutMs);
            await EnsureConnectedAsync(cts.Token).ConfigureAwait(false);
            _logger?.LogInformation(
                PrintingEventIds.TcpPrintStarted,
                "Warmup: pre-established persistent connection to TCP printer at {Host}:{Port}",
                _options.Host, _options.Port);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this client instance has already been disposed.</exception>
    public async Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (document.IsEmpty)
        {
            _logger?.LogDebug(
                PrintingEventIds.TcpPrintSuccess,
                "Empty print document '{DocumentName}' skipped.",
                document.DocumentName);
            return Result<bool>.Success(true);
        }

        _logger?.LogInformation(
            PrintingEventIds.TcpPrintStarted,
            "Initiating print job '{DocumentName}' to pooled TCP printer at {Host}:{Port}",
            document.DocumentName, _options.Host, _options.Port);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.TimeoutMs);

            await EnsureConnectedAsync(cts.Token).ConfigureAwait(false);

            await document.WriteToAsync(_stream!, cts.Token).ConfigureAwait(false);
            await _stream!.FlushAsync(cts.Token).ConfigureAwait(false);

            _logger?.LogInformation(
                PrintingEventIds.TcpPrintSuccess,
                "Successfully completed pooled print job '{DocumentName}' to TCP printer at {Host}:{Port}",
                document.DocumentName, _options.Host, _options.Port);

            return Result<bool>.Success(true);
        }
        catch (SocketException ex)
        {
            ResetConnection();
            _logger?.LogError(
                PrintingEventIds.TcpPrintFailed,
                ex, "Socket error sending pooled print job to TCP printer at {Host}:{Port}: {Message}",
                _options.Host, _options.Port, ex.Message);
            return Error.Unavailable(
                PrintingErrorCodes.SocketError,
                $"Failed to connect or send to printer at '{_options.Host}:{_options.Port}': {ex.Message}");
        }
        catch (IOException ex)
        {
            ResetConnection();
            _logger?.LogError(
                PrintingEventIds.TcpPrintFailed,
                ex, "Network I/O transmission error printing to pooled TCP printer at {Host}:{Port}: {Message}",
                _options.Host, _options.Port, ex.Message);
            return Error.Unavailable(
                PrintingErrorCodes.TransmissionError,
                $"Network IO error while sending print job to '{_options.Host}:{_options.Port}': {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            ResetConnection();

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
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client is { Connected: true } && _stream is not null)
        {
            if (!IsSocketDisconnected(_client.Client))
            {
                return;
            }
        }

        ResetConnection();

        var client = new TcpClient();
        client.NoDelay = _options.NoDelay;
        if (_options.LingerSeconds >= 0)
        {
            client.LingerState = new LingerOption(true, _options.LingerSeconds);
        }

        await client.ConnectAsync(_options.Host, _options.Port, cancellationToken).ConfigureAwait(false);

        _client = client;
        _stream = client.GetStream();
    }

    private static bool IsSocketDisconnected(Socket socket)
    {
        try
        {
            return socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private void ResetConnection()
    {
        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Suppress cleanup errors
        }
        finally
        {
            _stream = null;
        }

        try
        {
            _client?.Dispose();
        }
        catch
        {
            // Suppress cleanup errors
        }
        finally
        {
            _client = null;
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

        ResetConnection();
        // _gate.Dispose(); omitted intentionally to prevent ODE for concurrent waiters
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_stream != null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        _client?.Dispose();
        _client = null;
        // _gate.Dispose(); omitted intentionally to prevent ODE for concurrent waiters
    }
}





