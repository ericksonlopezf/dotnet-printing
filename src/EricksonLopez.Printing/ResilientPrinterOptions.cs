// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Printing;

/// <summary>
/// Specifies configuration options for resilient print operations and retry policies.
/// </summary>
public sealed class ResilientPrinterOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed print jobs. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial backoff delay before the first retry attempt. Defaults to 200 milliseconds.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum ceiling backoff delay between retry attempts. Defaults to 2 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the exponential backoff rate multiplier. Defaults to 2.0.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets a value indicating whether to retry when a transmission error occurs after data transmission has begun.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/> to prevent duplicate physical printouts when partial data has reached the printer.
    /// </remarks>
    public bool RetryOnTransmissionError { get; set; }
}
