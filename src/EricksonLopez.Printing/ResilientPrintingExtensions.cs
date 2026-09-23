// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing;

/// <summary>
/// Provides extension methods for decorating printer clients with resilience and retry capabilities.
/// </summary>
public static class ResilientPrintingExtensions
{
    /// <summary>
    /// Wraps the printer client in a <see cref="ResilientPrinterClient"/> to automatically retry on transient failures.
    /// </summary>
    /// <param name="client">The underlying printer client to decorate.</param>
    /// <param name="configure">The optional action to configure retry options.</param>
    /// <param name="logger">The optional logger for telemetry and diagnostics.</param>
    /// <param name="timeProvider">The optional time provider used for backoff delays.</param>
    /// <returns>A decorated <see cref="IPrinterClient"/> with retry support.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/></exception>
    public static IPrinterClient WithRetry(
        this IPrinterClient client,
        Action<ResilientPrinterOptions>? configure = null,
        ILogger<ResilientPrinterClient>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var options = new ResilientPrinterOptions();
        configure?.Invoke(options);

        return new ResilientPrinterClient(client, options, logger, timeProvider);
    }
}
