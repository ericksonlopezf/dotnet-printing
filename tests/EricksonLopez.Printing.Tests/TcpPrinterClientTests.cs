// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Tests;

public sealed class TcpPrinterClientTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new TcpPrinterClient(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public async Task PrintAsync_EmptyDocument_ReturnsSuccessImmediately()
    {
        var client = new TcpPrinterClient(new TcpPrinterClientOptions());
        var doc = new RawPrintDocument(Array.Empty<byte>());

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task PrintAsync_NullDocument_ThrowsArgumentNullException()
    {
        var client = new TcpPrinterClient(new TcpPrinterClientOptions());
        var act = () => client.PrintAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PrintAsync_ValidTcpServer_SendsExactPayloadAndReturnsSuccess()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var serverTask = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                using var stream = serverClient.GetStream();
                var buffer = new byte[100];
                var bytesRead = await stream.ReadAsync(buffer);
                return buffer[..bytesRead];
            });

            var logger = Substitute.For<ILogger<TcpPrinterClient>>();
            var client = new TcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            }, logger);

            byte[] payload = [0x1B, 0x40, 0x54, 0x45, 0x53, 0x54]; // ESC @ TEST
            var doc = new RawPrintDocument(payload, "TestJob");

            var result = await client.PrintAsync(doc);
            var receivedBytes = await serverTask;

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
            receivedBytes.Should().BeEquivalentTo(payload);

            logger.Received(1).Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Initiating print job")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());

            logger.Received(1).Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Successfully completed print job")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_UnreachableHost_ReturnsSocketErrorWithSpecificCode()
    {
        var logger = Substitute.For<ILogger<TcpPrinterClient>>();
        var client = new TcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "invalid_host_that_does_not_exist",
            Port = 9100,
            TimeoutMs = 5000
        }, logger);

        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Test"));

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SocketError");
        result.Error.Description.Should().StartWith("Failed to connect to printer at 'invalid_host_that_does_not_exist:9100':");

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Socket error connecting to TCP printer")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WithLogger_EmptyDocument_LogsDebugMessage()
    {
        var logger = Substitute.For<ILogger<TcpPrinterClient>>();
        var client = new TcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "192.168.1.10",
            Port = 9100
        }, logger);
        var doc = new RawPrintDocument(Array.Empty<byte>(), "EmptyDoc");

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Empty print document")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_WhenConnectingTimesOut_ReturnsTimeoutError()
    {
        var logger = Substitute.For<ILogger<TcpPrinterClient>>();
        var client = new TcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "10.255.255.1",
            Port = 9100,
            TimeoutMs = 15
        }, logger);

        var doc = new RawPrintDocument([0x1B, 0x40], "TimeoutDoc");

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().BeOneOf("Printer.Timeout", "Printer.SocketError");

        if (result.Error.Code == "Printer.Timeout")
        {
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Timed out after")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
