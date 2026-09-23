// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Printing;
using EricksonLopez.Result;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Tests;

public sealed class ResilientPrinterClientTests
{
    [Fact]
    public async Task PrintAsync_WhenInnerSucceeds_ReturnsSuccessImmediatelyWithoutRetry()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "TestDoc");

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Success(true)));

        var client = inner.WithRetry(options =>
        {
            options.MaxRetries = 3;
            options.InitialDelay = TimeSpan.FromMilliseconds(1);
        });

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrintAsync_WhenInnerFailsWithTransientError_RetriesUpToMaxRetriesAndSucceeds()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "TestDoc");

        var callCount = 0;
        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return Task.FromResult(Result<bool>.Failure(Error.Unavailable(PrintingErrorCodes.SocketError, "Printer busy")));
                }
                return Task.FromResult(Result<bool>.Success(true));
            });

        var client = inner.WithRetry(options =>
        {
            options.MaxRetries = 3;
            options.InitialDelay = TimeSpan.FromMilliseconds(1);
        });

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task PrintAsync_WhenInnerExceedsMaxRetries_ReturnsLastFailure()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "TestDoc");

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Unavailable("Printer.SocketError", "Host down"))));

        var client = inner.WithRetry(options =>
        {
            options.MaxRetries = 2;
            options.InitialDelay = TimeSpan.FromMilliseconds(1);
        });

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SocketError");
        await inner.Received(3).PrintAsync(doc, Arg.Any<CancellationToken>()); // 1 initial + 2 retries
    }

    [Fact]
    public async Task PrintAsync_WhenInnerFailsWithValidationError_DoesNotRetry()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "TestDoc");

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Validation("Printer.Invalid", "Bad payload"))));

        var client = inner.WithRetry(options =>
        {
            options.MaxRetries = 3;
            options.InitialDelay = TimeSpan.FromMilliseconds(1);
        });

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.Invalid");
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrintAsync_WhenNullDocument_ThrowsArgumentNullException()
    {
        var inner = Substitute.For<IPrinterClient>();
        var client = inner.WithRetry();

        var act = () => client.PrintAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullInnerClient_ThrowsArgumentNullException()
    {
        var act = () => new ResilientPrinterClient(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("innerClient");
    }

    [Fact]
    public async Task PrintAsync_WhenCancellationRequestedBeforeAttempt_ReturnsFailureImmediately()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "CancelDoc");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        inner.PrintAsync(doc, cts.Token)
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Unavailable("Printer.Timeout", "Timed out"))));

        var client = inner.WithRetry(options => options.MaxRetries = 3);
        var result = await client.PrintAsync(doc, cts.Token);

        result.IsFailure.Should().BeTrue();
        await inner.Received(1).PrintAsync(doc, cts.Token);
    }

    private sealed class CancellingTimeProvider : TimeProvider
    {
        private readonly CancellationTokenSource _cts;

        public CancellingTimeProvider(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _cts.Cancel();
            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingTimeProvider : TimeProvider
    {
        public System.Collections.Generic.List<TimeSpan> RecordedDelays { get; } = new();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            RecordedDelays.Add(dueTime);
            callback(state);
            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task PrintAsync_WhenCancellationOccursDuringDelay_CatchesAndReturnsLastResult()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "DelayCancelDoc");
        using var cts = new CancellationTokenSource();
        var timeProvider = new CancellingTimeProvider(cts);

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Unavailable(PrintingErrorCodes.SocketError, "Busy"))));

        var client = new ResilientPrinterClient(
            inner,
            new ResilientPrinterOptions
            {
                MaxRetries = 3,
                InitialDelay = TimeSpan.FromMilliseconds(50)
            },
            timeProvider: timeProvider);

        var result = await client.PrintAsync(doc, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.Canceled");
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrintAsync_WithLogger_LogsWarningOnTransientFailures()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "LogDoc");
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<ResilientPrinterClient>>();

        var attempts = 0;
        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return Task.FromResult(Result<bool>.Failure(Error.Unavailable("Printer.Busy", "Offline")));
                }
                return Task.FromResult(Result<bool>.Success(true));
            });

        var client = new ResilientPrinterClient(
            inner,
            new ResilientPrinterOptions
            {
                MaxRetries = 2,
                InitialDelay = TimeSpan.FromMilliseconds(1)
            },
            logger);

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(2);
        logger.Received(1).Log(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            Arg.Any<Microsoft.Extensions.Logging.EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("failed with error")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_ExponentialBackoffDelaysAndCap_CalculatedCorrectly()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "BackoffDoc");
        var timeProvider = new RecordingTimeProvider();

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Unavailable("Printer.Busy", "Offline"))));

        var client = new ResilientPrinterClient(
            inner,
            new ResilientPrinterOptions
            {
                MaxRetries = 3,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                BackoffMultiplier = 2.0,
                MaxDelay = TimeSpan.FromMilliseconds(300)
            },
            timeProvider: timeProvider);

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        await inner.Received(4).PrintAsync(doc, Arg.Any<CancellationToken>());
        timeProvider.RecordedDelays.Should().HaveCount(3);
        timeProvider.RecordedDelays[0].Should().Be(TimeSpan.FromMilliseconds(100));
        timeProvider.RecordedDelays[1].Should().Be(TimeSpan.FromMilliseconds(200));
        timeProvider.RecordedDelays[2].Should().Be(TimeSpan.FromMilliseconds(300));
    }
}

