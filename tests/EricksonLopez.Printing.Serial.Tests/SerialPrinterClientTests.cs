// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Result;
using Microsoft.Extensions.Logging;
using EricksonLopez.Printing.Serial;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Serial.Tests;

public sealed class SerialPrinterClientTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new SerialPrinterClient(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public async Task PrintAsync_NullDocument_ThrowsArgumentNullException()
    {
        var client = new SerialPrinterClient(new SerialPrinterClientOptions());
        var act = () => client.PrintAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PrintAsync_EmptyDocument_ReturnsSuccessImmediately()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM1" }, logger);
        var doc = new RawPrintDocument(Array.Empty<byte>(), "EmptyReceipt");

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Empty print document")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_NonExistentPort_ReturnsUnavailableError()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions
        {
            PortName = "COM999",
            BaudRate = 9600,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            WriteTimeoutMs = 500
        }, logger);

        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Test"), "Receipt999");

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().BeOneOf("Printer.SerialIoError", "Printer.SerialAccessDenied");
        result.Error.Description.Should().Contain("COM999");

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Initiating print job")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("error") || o.ToString()!.Contains("denied")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_InvalidPortParameters_ReturnsValidationError()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions
        {
            PortName = "COM1",
            BaudRate = -1 // Invalid baud rate causes ArgumentException in SerialPort constructor
        }, logger);

        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Data"), "BadBaud");

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.InvalidPortName");
        result.Error.Description.Should().Contain("COM1");
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Invalid serial port parameter")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WithStreamFactory_WritesAndFlushesSuccessfully()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM1" }, logger);

        FlushTrackingStream? trackingStream = null;
        client.StreamFactory = _ =>
        {
            trackingStream = new FlushTrackingStream();
            return trackingStream;
        };

        var docBytes = Encoding.UTF8.GetBytes("PrintThisData");
        var doc = new RawPrintDocument(docBytes, "SuccessReceipt");

        var syncContext = new DisallowingSynchronizationContext();
        Result<bool> result = default!;

        await Task.Run(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                result = await client.PrintAsync(doc).ConfigureAwait(false);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        trackingStream.Should().NotBeNull();
        trackingStream!.ToArray().Should().Equal(docBytes);
        trackingStream.FlushCount.Should().Be(1);
        syncContext.WasViolated.Should().BeFalse();

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully completed print job")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WithStreamFactory_ThrowsTimeoutException_ReturnsSerialTimeout()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM2" }, logger);
        client.StreamFactory = _ => throw new TimeoutException("Write timed out");

        var doc = new RawPrintDocument([0x1B, 0x40], "TimeoutDoc");
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SerialTimeout");
        result.Error.Description.Should().Contain("COM2");
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Timeout communicating")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WithStreamFactory_ThrowsInvalidOperationException_ReturnsSerialInvalidState()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM3" }, logger);
        client.StreamFactory = _ => throw new InvalidOperationException("Port already open");

        var doc = new RawPrintDocument([0x1B, 0x40], "InvalidOpDoc");
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SerialInvalidState");
        result.Error.Description.Should().Contain("COM3");
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("invalid state")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WithStreamFactory_ThrowsUnauthorizedAccessException_ReturnsSerialAccessDenied()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM4" }, logger);
        client.StreamFactory = _ => throw new UnauthorizedAccessException("Access denied to port");

        var doc = new RawPrintDocument([0x1B, 0x40], "DeniedDoc");
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SerialAccessDenied");
        result.Error.Description.Should().Contain("COM4");

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Access denied to serial port 'COM4'")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WithStreamFactory_ThrowsIOException_ReturnsSerialIoError()
    {
        var logger = Substitute.For<ILogger<SerialPrinterClient>>();
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM5" }, logger);
        client.StreamFactory = _ => throw new IOException("I/O failure");

        var doc = new RawPrintDocument([0x1B, 0x40], "IoDoc");
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SerialIoError");
        result.Error.Description.Should().Contain("COM5");
    }

    [Fact]
    public async Task PrintAsync_WithDisallowingSynchronizationContext_DoesNotPostToSynchronizationContext()
    {
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM1" });
        var syncContext = new DisallowingSynchronizationContext();

        FlushTrackingStream? trackingStream = null;
        client.StreamFactory = _ =>
        {
            trackingStream = new FlushTrackingStream(syncContextToSet: syncContext) { YieldOnFlush = true };
            return trackingStream;
        };

        var doc = new YieldingPrintDocument([0x1B, 0x40], "YieldDoc", delayMs: 10, syncContextToSet: syncContext);

        // 1. Hold gate via concurrent print job
        var gateAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var blockingDoc = new DelegatingPrintDocument(async (s, ct) =>
        {
            gateAcquired.SetResult(true);
            await releaseGate.Task.ConfigureAwait(false);
        });

        var blockingTask = Task.Run(() => client.PrintAsync(blockingDoc));
        await gateAcquired.Task;

        // Now _gate is held. Next print must wait asynchronously on _gate.WaitAsync()
        Result<bool> result = default!;
        await Task.Run(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                var printTask = client.PrintAsync(doc);
                releaseGate.SetResult(true);
                result = await printTask.ConfigureAwait(false);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        });

        await blockingTask;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        syncContext.WasViolated.Should().BeFalse();
    }

    [Fact]
    public async Task PrintAsync_SequentialPrints_ReleasesGatePromptly()
    {
        var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM1" });
        client.StreamFactory = _ => new FlushTrackingStream();

        var doc = new RawPrintDocument([0x1B, 0x40], "Doc");
        var res1 = await client.PrintAsync(doc);
        res1.IsSuccess.Should().BeTrue();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var res2 = await client.PrintAsync(doc, cts.Token);
        res2.IsSuccess.Should().BeTrue();
    }
}


