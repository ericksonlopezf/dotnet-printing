// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Printing;
using EricksonLopez.Result;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Tests;

/// <summary>
/// Concurrency, Chaos, and Idempotency verification suite for core Printing engine.
/// Proves thread-safety invariants, cancellation enforcement, and fault recovery.
/// </summary>
public sealed class AuditConcurrencyAndChaosTests
{
    [Fact]
    public async Task Concurrency_TcpPrinterClient_ConcurrentCalls_ExecuteWithoutDeadlocks()
    {
        // Target an unopen local port to verify all concurrent callers fail gracefully
        var options = new TcpPrinterClientOptions
        {
            Host = "127.0.0.1",
            Port = 49152, // high unused port
            TimeoutMs = 150
        };
        var client = new TcpPrinterClient(options, NullLogger<TcpPrinterClient>.Instance);
        var doc = new RawPrintDocument([0x1B, 0x40], "ConcurrencyTest");

        var tasks = new Task<Result<bool>>[20];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = client.PrintAsync(doc);
        }

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            result.IsFailure.Should().BeTrue();
            (result.Error.Code == "Printer.SocketError" || result.Error.Code == "Printer.Timeout")
                .Should().BeTrue();
        }
    }

    [Fact]
    public async Task Chaos_ResilientPrinterClient_CancellationDuringBackoff_AbortsCleanly()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "CancelDuringDelay");

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Unavailable("Printer.SocketError", "Unavailable"))));

        using var cts = new CancellationTokenSource();
        var client = new ResilientPrinterClient(inner, new ResilientPrinterOptions
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromSeconds(5) // long delay
        });

        var printTask = client.PrintAsync(doc, cts.Token);
        // Cancel after 50ms while inside Task.Delay
        cts.CancelAfter(50);

        var result = await printTask;
        result.IsFailure.Should().BeTrue();
        // Inner client must not have performed all 5 retries
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Idempotency_ResilientPrinterClient_PreventsDuplicatePrint_OnTransmissionErrorByDefault()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "ReceiptJob");

        // Simulate network drop mid-transmission
        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Unavailable("Printer.TransmissionError", "Broken pipe"))));

        var client = new ResilientPrinterClient(inner, new ResilientPrinterOptions
        {
            MaxRetries = 3,
            RetryOnTransmissionError = false // default
        });

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.TransmissionError");
        // Must strictly receive only 1 call to prevent physical paper waste / duplicate ticket
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }
}
