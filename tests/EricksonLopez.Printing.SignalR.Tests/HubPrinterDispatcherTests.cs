// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.SignalR.Tests;

public sealed class HubPrinterDispatcherTests
{
    private readonly IHubContext<PrinterHub, IPrinterHubClient> _hubContext = Substitute.For<IHubContext<PrinterHub, IPrinterHubClient>>();
    private readonly IHubClients<IPrinterHubClient> _hubClients = Substitute.For<IHubClients<IPrinterHubClient>>();
    private readonly IPrinterHubClient _clientProxy = Substitute.For<IPrinterHubClient>();

    public HubPrinterDispatcherTests()
    {
        _hubContext.Clients.Returns(_hubClients);
        _hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime;
        public FixedTimeProvider(DateTimeOffset fixedTime) => _fixedTime = fixedTime;
        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }

    [Fact]
    public async Task DispatchAsync_ValidDocument_SendsPrintJobMessageToGroup()
    {
        var fixedTime = new DateTimeOffset(2026, 9, 4, 15, 30, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedTime);
        var logger = Substitute.For<ILogger<HubPrinterDispatcher>>();
        var dispatcher = new HubPrinterDispatcher(_hubContext, timeProvider, logger);
        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Test"), "Receipt");

        var result = await dispatcher.DispatchAsync("POS-01", doc);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _hubClients.Received(1).Group("printer:POS-01");
        await _clientProxy.Received(1).OnPrintJobReceived(
            Arg.Is<PrintJobMessage>(m =>
                m.JobId.Length == 32 &&
                m.PrinterName == "POS-01" &&
                m.DocumentName == "Receipt" &&
                m.PayloadBase64 == Convert.ToBase64String(Encoding.UTF8.GetBytes("Test")) &&
                m.CreatedAt == fixedTime));

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Dispatching print job")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully dispatched print job")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispatchAsync_NullOrWhitespacePrinterName_ReturnsValidationError(string? invalidName)
    {
        var logger = Substitute.For<ILogger<HubPrinterDispatcher>>();
        var dispatcher = new HubPrinterDispatcher(_hubContext, null, logger);
        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Test"));

        var result = await dispatcher.DispatchAsync(invalidName!, doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.InvalidName");
        result.Error.Description.Should().Be("Printer name cannot be null or empty.");
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Dispatch rejected")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DispatchAsync_NullDocument_ThrowsArgumentNullException()
    {
        var dispatcher = new HubPrinterDispatcher(_hubContext);
        var act = () => dispatcher.DispatchAsync("POS-01", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DispatchAsync_WhenHubThrowsException_ReturnsUnavailableError()
    {
        var logger = Substitute.For<ILogger<HubPrinterDispatcher>>();
        _clientProxy.When(c => c.OnPrintJobReceived(Arg.Any<PrintJobMessage>()))
            .Do(_ => throw new InvalidOperationException("SignalR connection pool exhausted"));

        var dispatcher = new HubPrinterDispatcher(_hubContext, null, logger);
        var doc = new RawPrintDocument([0x1B, 0x40], "Receipt");

        var result = await dispatcher.DispatchAsync("POS-01", doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.DispatchError");
        result.Error.Description.Should().Contain("SignalR connection pool exhausted");
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to dispatch print job")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DispatchAsync_WithIdempotencyKey_PropagatesJobIdDirectly()
    {
        var fixedTime = new DateTimeOffset(2026, 9, 4, 15, 30, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedTime);
        var dispatcher = new HubPrinterDispatcher(_hubContext, timeProvider);
        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Test"), "Receipt", "idem-uuid-999");

        var result = await dispatcher.DispatchAsync("POS-01", doc);

        result.IsSuccess.Should().BeTrue();
        await _clientProxy.Received(1).OnPrintJobReceived(
            Arg.Is<PrintJobMessage>(m =>
                m.JobId == "idem-uuid-999" &&
                m.PrinterName == "POS-01" &&
                m.DocumentName == "Receipt"));
    }

    [Fact]
    public async Task DispatchAsync_PreCancelledToken_ThrowsOperationCanceledExceptionImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dispatcher = new HubPrinterDispatcher(_hubContext);
        var doc = new RawPrintDocument([0x1B, 0x40], "Receipt");

        var act = () => dispatcher.DispatchAsync("POS-01", doc, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _clientProxy.DidNotReceiveWithAnyArgs().OnPrintJobReceived(default!);
    }

    [Fact]
    public async Task DispatchAsync_ConfiguresAwaitFalse()
    {
        var syncContext = new DisallowingSynchronizationContext();
        var yieldingClient = Substitute.For<IPrinterHubClient>();
        yieldingClient.OnPrintJobReceived(Arg.Any<PrintJobMessage>()).Returns(_ => Task.Run(async () =>
        {
            await Task.Delay(10).ConfigureAwait(false);
        }));

        var hubClients = Substitute.For<IHubClients<IPrinterHubClient>>();
        hubClients.Group(Arg.Any<string>()).Returns(yieldingClient);
        var hubContext = Substitute.For<IHubContext<PrinterHub, IPrinterHubClient>>();
        hubContext.Clients.Returns(hubClients);

        var dispatcher = new HubPrinterDispatcher(hubContext);
        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Test"), "Receipt");

        await Task.Run(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                var result = await dispatcher.DispatchAsync("POS-01", doc).ConfigureAwait(false);
                result.IsSuccess.Should().BeTrue();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        });

        syncContext.WasViolated.Should().BeFalse();
    }
}
