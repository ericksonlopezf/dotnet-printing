// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO.Ports;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Printing;
using EricksonLopez.Printing.Serial;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Serial.Tests;

/// <summary>
/// Hardening, concurrency, options, and dependency injection test suite for the Serial COM driver.
/// </summary>
public sealed class SerialAuditTests
{
    [Fact]
    public void SerialPrinterClientOptions_Properties_GetAndSetProperly()
    {
        var defaultOpt = new SerialPrinterClientOptions();
        defaultOpt.PortName.Should().Be("COM1");
        defaultOpt.BaudRate.Should().Be(9600);
        defaultOpt.Parity.Should().Be(Parity.None);
        defaultOpt.DataBits.Should().Be(8);
        defaultOpt.StopBits.Should().Be(StopBits.One);
        defaultOpt.Handshake.Should().Be(Handshake.None);
        defaultOpt.WriteTimeoutMs.Should().Be(5000);
        defaultOpt.ReadTimeoutMs.Should().Be(2000);

        var options = new SerialPrinterClientOptions
        {
            PortName = "COM3",
            BaudRate = 38400,
            Parity = Parity.Even,
            DataBits = 7,
            StopBits = StopBits.Two,
            Handshake = Handshake.RequestToSend,
            WriteTimeoutMs = 2000,
            ReadTimeoutMs = 1500
        };

        options.PortName.Should().Be("COM3");
        options.BaudRate.Should().Be(38400);
        options.Parity.Should().Be(Parity.Even);
        options.DataBits.Should().Be(7);
        options.StopBits.Should().Be(StopBits.Two);
        options.Handshake.Should().Be(Handshake.RequestToSend);
        options.WriteTimeoutMs.Should().Be(2000);
        options.ReadTimeoutMs.Should().Be(1500);
    }

    [Fact]
    public void AddSerialPrinter_NullArguments_ThrowsArgumentNullException()
    {
        var services = Substitute.For<IServiceCollection>();
        var configured = false;

        var actNullServices = () => PrintingSerialServiceCollectionExtensions.AddSerialPrinter(null!, _ => { configured = true; });
        var actNullConfigure = () => services.AddSerialPrinter(null!);
        var actBothNull = () => PrintingSerialServiceCollectionExtensions.AddSerialPrinter(null!, null!);

        actNullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configured.Should().BeFalse();
        actNullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
        actBothNull.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddSerialPrinter_ValidRegistration_RegistersSingletonAndFactoryResolvesClient()
    {
        ServiceDescriptor? capturedDescriptor = null;
        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci => capturedDescriptor = ci.Arg<ServiceDescriptor>());

        services.AddSerialPrinter(opt =>
        {
            opt.PortName = "COM2";
            opt.BaudRate = 115200;
            opt.WriteTimeoutMs = 1000;
        });

        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.ServiceType.Should().Be<IPrinterClient>();
        capturedDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var sp = Substitute.For<IServiceProvider>();
        var client = capturedDescriptor.ImplementationFactory!(sp);
        client.Should().NotBeNull();
        client.Should().BeOfType<SerialPrinterClient>();

        var optionsField = typeof(SerialPrinterClient).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var resolvedOptions = optionsField?.GetValue(client) as SerialPrinterClientOptions;
        resolvedOptions.Should().NotBeNull();
        resolvedOptions!.PortName.Should().Be("COM2");
        resolvedOptions.BaudRate.Should().Be(115200);
        resolvedOptions.WriteTimeoutMs.Should().Be(1000);
    }

    [Fact]
    public void FIND_PRINT_010_SerialServiceCollectionExtensions_AddKeyedSerialPrinter_RegistersKeyedSingleton()
    {
        ServiceDescriptor? serialDescriptor = null;

        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci =>
            {
                var desc = ci.Arg<ServiceDescriptor>();
                if (Equals(desc.ServiceKey, "FrontDesk"))
                {
                    serialDescriptor = desc;
                }
            });

        services.AddKeyedSerialPrinter("FrontDesk", opt =>
        {
            opt.PortName = "COM3";
            opt.BaudRate = 19200;
        });

        serialDescriptor.Should().NotBeNull();
        serialDescriptor!.IsKeyedService.Should().BeTrue();
        serialDescriptor.ServiceKey.Should().Be("FrontDesk");
        serialDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var sp = Substitute.For<IServiceProvider>();
        var clientObj = serialDescriptor.KeyedImplementationFactory!(sp, "FrontDesk");
        clientObj.Should().BeOfType<SerialPrinterClient>();

        var optionsField = typeof(SerialPrinterClient).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var resolvedOptions = optionsField?.GetValue(clientObj) as SerialPrinterClientOptions;
        resolvedOptions.Should().NotBeNull();
        resolvedOptions!.PortName.Should().Be("COM3");
        resolvedOptions.BaudRate.Should().Be(19200);
    }

    [Fact]
    public void AddKeyedSerialPrinter_NullArguments_ThrowsArgumentNullException()
    {
        var services = Substitute.For<IServiceCollection>();
        var configured = false;

        var actNullServices = () => PrintingSerialServiceCollectionExtensions.AddKeyedSerialPrinter(null!, "key", _ => { configured = true; });
        var actNullKey = () => services.AddKeyedSerialPrinter(null!, _ => { });
        var actNullConfigure = () => services.AddKeyedSerialPrinter("key", null!);

        actNullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configured.Should().BeFalse();
        actNullKey.Should().Throw<ArgumentNullException>().WithParameterName("serviceKey");
        actNullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public async Task FIND_PRINT_011_SerialPrinterClient_CatchesInvalidOperationException_ReturnsUnavailable()
    {
        var options = new SerialPrinterClientOptions { PortName = "COM1" };
        var client = new SerialPrinterClient(options);
        client.StreamFactory = _ => throw new InvalidOperationException("Port in invalid state");

        var doc = new RawPrintDocument([0x1B, 0x40], "Doc");
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SerialInvalidState");
        result.Error.Description.Should().Contain("COM1");
    }

    [Fact]
    public async Task FIND_PRINT_020_SerialPrinterClient_DisposedClient_ThrowsObjectDisposedException()
    {
        var options = new SerialPrinterClientOptions { PortName = "COM1" };
        var client = new SerialPrinterClient(options);
        client.Dispose();
        client.Dispose(); // Verify idempotency / early return

        var doc = new RawPrintDocument([0x1B, 0x40], "Doc");
        var act = () => client.PrintAsync(doc);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Concurrency_SerialPrinterClient_SerializesAccessWithoutCrashing()
    {
        using var client = new SerialPrinterClient(new SerialPrinterClientOptions { PortName = "COM99" });
        var doc = new RawPrintDocument([0x1B, 0x40], "SerialConcurrent");

        var tasks = new Task<Result<bool>>[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = client.PrintAsync(doc);
        }

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().BeOneOf(
                "Printer.SerialIoError",
                "Printer.SerialAccessDenied",
                "Printer.InvalidPortName",
                "Printer.SerialInvalidState",
                "Printer.SerialTimeout");
        }
    }
}
