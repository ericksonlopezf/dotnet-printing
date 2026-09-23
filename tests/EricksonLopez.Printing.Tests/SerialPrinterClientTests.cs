// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using EricksonLopez.Printing.Serial;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Tests;

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
}

