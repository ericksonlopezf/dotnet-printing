// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing;

/// <summary>
/// Provides resilient retry and exponential backoff capabilities by decorating an underlying <see cref="IPrinterClient"/>.
/// </summary>
public sealed class ResilientPrinterClient : IPrinterClient
{
    private readonly IPrinterClient _innerClient;
    private readonly ResilientPrinterOptions _options;
    private readonly ILogger<ResilientPrinterClient>? _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientPrinterClient"/> class with the specified client, options, logger, and time provider.
    /// </summary>
    /// <param name="innerClient">The underlying printer client to decorate.</param>
    /// <param name="options">The retry policy configuration options.</param>
    /// <param name="logger">The optional logger for telemetry and diagnostics.</param>
    /// <param name="timeProvider">The optional time provider used for backoff delays.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerClient"/> is <see langword="null"/></exception>
    public ResilientPrinterClient(
        IPrinterClient innerClient,
        ResilientPrinterOptions? options = null,
        ILogger<ResilientPrinterClient>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _options = options ?? new ResilientPrinterOptions();
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var attempts = 0;
        var delay = _options.InitialDelay;

        while (true)
        {
            attempts++;
            Result<bool> result;
            try
            {
                result = await _innerClient.PrintAsync(document, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Error.Unavailable(
                    PrintingErrorCodes.Canceled,
                    "Print operation was canceled by the caller.");
            }

            if (result.IsSuccess || cancellationToken.IsCancellationRequested)
            {
                return result;
            }

            if (attempts > _options.MaxRetries)
            {
                _logger?.LogError(
                    PrintingEventIds.RetryExhausted,
                    "All {MaxRetries} retry attempts exhausted for print job '{DocumentName}'. Last error: [{ErrorCode}] {ErrorMessage}",
                    _options.MaxRetries, document.DocumentName, result.Error.Code, result.Error.Description);
                return result;
            }

            // Do not retry deterministic validation errors
            if (result.Error.Type == ErrorType.Validation)
            {
                return result;
            }

            if ((!document.IsIdempotent && !_options.RetryOnTransmissionError) &&
                (result.Error.Code == PrintingErrorCodes.TransmissionError || result.Error.Code == PrintingErrorCodes.Timeout))
            {
                _logger?.LogWarning(
                    PrintingEventIds.RetryExhausted,
                    "Skipping retry for '{ErrorCode}' on print job '{DocumentName}' to prevent duplicate physical printing.",
                    result.Error.Code, document.DocumentName);
                return result;
            }

            _logger?.LogWarning(
                PrintingEventIds.RetryAttempt,
                "Print attempt {Attempt} of {MaxRetries} failed with error '{ErrorCode}': {ErrorMessage}. Retrying in {DelayMs}ms...",
                attempts, _options.MaxRetries, result.Error.Code, result.Error.Description, delay.TotalMilliseconds);

            try
            {
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Error.Unavailable(PrintingErrorCodes.Canceled, "Print operation was canceled during retry backoff.");
            }

            var nextDelayMs = delay.TotalMilliseconds * _options.BackoffMultiplier;
            delay = TimeSpan.FromMilliseconds(Math.Min(nextDelayMs, _options.MaxDelay.TotalMilliseconds));
        }
    }
}

