// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Result;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates error classification taxonomy, exponential backoff retries, and physical idempotency protection.
/// </summary>
public static class Level06ErrorHandlingAndClassification
{
    /// <summary>
    /// Executes the error handling and resilience showcase demonstration asynchronously.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 06: ERROR HANDLING, RESILIENCE & PHYSICAL IDEMPOTENCY");
        Console.WriteLine("================================================================================\n");

        // 1. Error Codes Taxonomy
        Console.WriteLine("[1] Standard Printing Error Taxonomy (PrintingErrorCodes):");
        Console.WriteLine($"    • Socket Connection Failure: '{PrintingErrorCodes.SocketError}'");
        Console.WriteLine($"    • Mid-Stream Transmission Error: '{PrintingErrorCodes.TransmissionError}'");
        Console.WriteLine($"    • Network/Operation Timeout: '{PrintingErrorCodes.Timeout}'");
        Console.WriteLine($"    • Caller Cancellation: '{PrintingErrorCodes.Canceled}'\n");

        // 2. Resilient Retries with Exponential Backoff
        Console.WriteLine("[2] Resilient Retry Decorator with Exponential Backoff (Simulating Transient Socket Drops):");
        var attemptsCount = 0;
        var mockTransientClient = new TestMockPrinterClient(doc =>
        {
            attemptsCount++;
            if (attemptsCount < 3)
            {
                return Task.FromResult<Result<bool>>(Error.Unavailable(
                    PrintingErrorCodes.SocketError,
                    $"Transient network drop on attempt #{attemptsCount}"));
            }
            return Task.FromResult(Result<bool>.Success(true));
        });

        var resilientClient = mockTransientClient.WithRetry(options =>
        {
            options.MaxRetries = 3;
            options.InitialDelay = TimeSpan.FromMilliseconds(50);
            options.BackoffMultiplier = 2.0;
            options.MaxDelay = TimeSpan.FromMilliseconds(500);
        });

        var sampleDoc = new RawPrintDocument([0x1B, 0x40], "TransientTestDoc", isIdempotent: true);
        var retryResult = await resilientClient.PrintAsync(sampleDoc);

        Console.WriteLine($"    Attempts executed: {attemptsCount}");
        Console.WriteLine($"    Final Result: IsSuccess={retryResult.IsSuccess}, Value={retryResult.Value}");
        Console.WriteLine("    ✔ ResilientPrinterClient automatically recovered after transient socket failures.\n");

        // 3. Physical Idempotency Invariant: Preventing Duplicate Printed Paper
        Console.WriteLine("[3] Physical Idempotency Protection Rule:");
        Console.WriteLine("    (If data transmission started and failed mid-stream, retrying non-idempotent documents is FORBIDDEN)");

        var nonIdempotentDoc = new RawPrintDocument([0x1B, 0x40], "NonIdempotentBill", isIdempotent: false);
        var transmissionFailCount = 0;
        var mockTransmissionErrorClient = new TestMockPrinterClient(doc =>
        {
            transmissionFailCount++;
            return Task.FromResult<Result<bool>>(Error.Unavailable(
                PrintingErrorCodes.TransmissionError,
                "TCP connection reset after 500 bytes sent."));
        });

        var nonIdempotentResilient = mockTransmissionErrorClient.WithRetry(options =>
        {
            options.MaxRetries = 3;
            options.RetryOnTransmissionError = false; // Default safe setting
        });

        var transmissionResult = await nonIdempotentResilient.PrintAsync(nonIdempotentDoc);
        Console.WriteLine($"    Attempts executed: {transmissionFailCount}");
        Console.WriteLine($"    Result IsFailure: {transmissionResult.IsFailure}");
        Console.WriteLine($"    Error Code: {transmissionResult.Error.Code}");
        Console.WriteLine($"    Description: {transmissionResult.Error.Description}");
        Console.WriteLine("    ✔ Retry was aborted immediately to prevent physical duplicate printing at the customer checkout.\n");

        // 4. Deterministic Validation Errors (ErrorType.Validation are NEVER retried)
        Console.WriteLine("[4] Non-transient Validation Errors (Bypass retry loop entirely):");
        var validationFailCount = 0;
        var mockValidationClient = new TestMockPrinterClient(doc =>
        {
            validationFailCount++;
            return Task.FromResult<Result<bool>>(Error.Validation("Printer.InvalidPayload", "Payload corrupted."));
        });

        var validationResilient = mockValidationClient.WithRetry(options => options.MaxRetries = 5);
        var valResult = await validationResilient.PrintAsync(sampleDoc);
        Console.WriteLine($"    Attempts executed: {validationFailCount} (Expected: exactly 1)");
        Console.WriteLine($"    Result Code: {valResult.Error.Code}, Type: {valResult.Error.Type}");
        Console.WriteLine("    ✔ Validation errors skip retries to avoid wasting compute/network resources.");

        Console.WriteLine("\n✔ Level 06 completed successfully.\n");
    }

    private sealed class TestMockPrinterClient : IPrinterClient
    {
        private readonly Func<IPrintDocument, Task<Result<bool>>> _handler;

        public TestMockPrinterClient(Func<IPrintDocument, Task<Result<bool>>> handler)
        {
            _handler = handler;
        }

        public Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
        {
            return _handler(document);
        }
    }
}
