// Copyright © Erickson Lopez. MIT License.
namespace EricksonLopez.Printing.Tests;

using System;
using AwesomeAssertions;
using EricksonLopez.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public sealed class PrintingCoreTests
{
    private sealed class CustomDocumentWithoutGetMemoryOverride : IPrintDocument
    {
        public string DocumentName => "CustomNoOverride";
        public byte[] GetBytes() => [0x1B, 0x40, 0x0A];
        public System.Threading.Tasks.ValueTask WriteToAsync(System.IO.Stream stream, System.Threading.CancellationToken cancellationToken = default) => new();
    }

    [Fact]
    public void IPrintDocument_DefaultGetMemory_ReturnsWrappingReadOnlyMemory()
    {
        IPrintDocument doc = new CustomDocumentWithoutGetMemoryOverride();
        var memory = doc.GetMemory();

        memory.ToArray().Should().BeEquivalentTo(new byte[] { 0x1B, 0x40, 0x0A });
    }

    [Fact]
    public void RawPrintDocument_ValidConstructor_InitializesProperties()
    {
        byte[] payload = [0x10, 0x04, 0x01];
        var doc = new RawPrintDocument(payload, "MyReceipt");

        doc.DocumentName.Should().Be("MyReceipt");
        doc.GetBytes().Should().BeSameAs(payload);
        doc.GetMemory().ToArray().Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void RawPrintDocument_DefaultDocumentName_IsPrintJob()
    {
        byte[] payload = [0x1B, 0x64, 0x01];
        var doc = new RawPrintDocument(payload);

        doc.DocumentName.Should().Be("PrintJob");
    }

    [Fact]
    public void RawPrintDocument_NullArguments_ThrowsArgumentNullException()
    {
        var actNullBytes = () => new RawPrintDocument(null!, "Name");
        var actNullName = () => new RawPrintDocument([0x01], null!);

        actNullBytes.Should().Throw<ArgumentNullException>().WithParameterName("bytes");
        actNullName.Should().Throw<ArgumentNullException>().WithParameterName("documentName");
    }

    [Fact]
    public void AddTcpPrinter_NullArguments_ThrowsArgumentNullException()
    {
        var services = Substitute.For<IServiceCollection>();

        var actNullServices = () => PrintingServiceCollectionExtensions.AddTcpPrinter(null!, _ => { });
        var actNullConfigure = () => services.AddTcpPrinter(null!);
        var actBothNull = () => PrintingServiceCollectionExtensions.AddTcpPrinter(null!, null!);

        actNullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        actNullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
        actBothNull.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddTcpPrinter_ValidRegistration_RegistersSingletonAndFactoryResolvesClient()
    {
        ServiceDescriptor? capturedDescriptor = null;
        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci => capturedDescriptor = ci.Arg<ServiceDescriptor>());

        services.AddTcpPrinter(opt =>
        {
            opt.Host = "192.168.1.50";
            opt.Port = 9200;
            opt.TimeoutMs = 1500;
        });

        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.ServiceType.Should().Be<IPrinterClient>();
        capturedDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var sp = Substitute.For<IServiceProvider>();
        var client = capturedDescriptor.ImplementationFactory!(sp);
        client.Should().NotBeNull();
        client.Should().BeOfType<TcpPrinterClient>();

        var optionsField = typeof(TcpPrinterClient).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var resolvedOptions = optionsField?.GetValue(client) as TcpPrinterClientOptions;
        resolvedOptions.Should().NotBeNull();
        resolvedOptions!.Host.Should().Be("192.168.1.50");
        resolvedOptions.Port.Should().Be(9200);
        resolvedOptions.TimeoutMs.Should().Be(1500);
    }



    [Fact]
    public void ResilientPrintingExtensions_WithRetry_NullClient_ThrowsArgumentNullException()
    {
        IPrinterClient nullClient = null!;
        var act = () => nullClient.WithRetry();

        act.Should().Throw<ArgumentNullException>().WithParameterName("client");
    }

    [Fact]
    public void ResilientPrinterOptions_DefaultAndCustomValues_SetCorrectly()
    {
        var options = new ResilientPrinterOptions();
        options.MaxRetries.Should().Be(3);
        options.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        options.MaxDelay.Should().Be(TimeSpan.FromMilliseconds(2000));
        options.BackoffMultiplier.Should().Be(2.0);

        options.MaxRetries = 5;
        options.InitialDelay = TimeSpan.FromMilliseconds(50);
        options.MaxDelay = TimeSpan.FromMilliseconds(5000);
        options.BackoffMultiplier = 1.5;

        options.MaxRetries.Should().Be(5);
        options.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(50));
        options.MaxDelay.Should().Be(TimeSpan.FromMilliseconds(5000));
        options.BackoffMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void TcpPrinterClientOptions_Properties_GetAndSetProperly()
    {
        var defaultOpt = new TcpPrinterClientOptions();
        defaultOpt.Host.Should().Be("127.0.0.1");
        defaultOpt.Port.Should().Be(9100);
        defaultOpt.TimeoutMs.Should().Be(5000);

        var options = new TcpPrinterClientOptions
        {
            Host = "10.0.0.1",
            Port = 9101,
            TimeoutMs = 3000
        };

        options.Host.Should().Be("10.0.0.1");
        options.Port.Should().Be(9101);
        options.TimeoutMs.Should().Be(3000);
    }



    [Fact]
    public void TcpPrinterClientOptions_SocketOptions_DefaultAndCustomValues()
    {
        var defaultOpt = new TcpPrinterClientOptions();
        defaultOpt.NoDelay.Should().BeTrue();
        defaultOpt.LingerSeconds.Should().Be(0);

        var customOpt = new TcpPrinterClientOptions
        {
            NoDelay = false,
            LingerSeconds = 5
        };

        customOpt.NoDelay.Should().BeFalse();
        customOpt.LingerSeconds.Should().Be(5);
    }

    [Fact]
    public void AddKeyedTcpPrinter_NullArguments_ThrowsArgumentNullException()
    {
        var services = Substitute.For<IServiceCollection>();
        var configureCalled = false;

        var actNullServices = () => PrintingServiceCollectionExtensions.AddKeyedTcpPrinter(null!, "key", _ => configureCalled = true);
        var actNullKey = () => services.AddKeyedTcpPrinter(null!, _ => configureCalled = true);
        var actNullConfigure = () => services.AddKeyedTcpPrinter("key", null!);

        actNullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configureCalled.Should().BeFalse();

        actNullKey.Should().Throw<ArgumentNullException>().WithParameterName("serviceKey");
        configureCalled.Should().BeFalse();

        actNullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void AddKeyedTcpPrinter_ValidRegistration_RegistersKeyedSingletonAndConfiguresOptions()
    {
        ServiceDescriptor? capturedDescriptor = null;
        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci => capturedDescriptor = ci.Arg<ServiceDescriptor>());

        services.AddKeyedTcpPrinter("kitchen-printer", opt =>
        {
            opt.Host = "10.0.0.25";
            opt.Port = 9100;
            opt.TimeoutMs = 4500;
        });

        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.ServiceType.Should().Be<IPrinterClient>();
        capturedDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        capturedDescriptor.ServiceKey.Should().Be("kitchen-printer");

        var sp = Substitute.For<IServiceProvider>();
        var client = capturedDescriptor.KeyedImplementationFactory!(sp, "kitchen-printer") as TcpPrinterClient;
        client.Should().NotBeNull();
        client!.Options.Host.Should().Be("10.0.0.25");
        client.Options.Port.Should().Be(9100);
        client.Options.TimeoutMs.Should().Be(4500);
    }

    [Fact]
    public void AddPooledTcpPrinter_NullArguments_ThrowsArgumentNullException()
    {
        var services = Substitute.For<IServiceCollection>();
        var configureCalled = false;

        var actNullServices = () => PrintingServiceCollectionExtensions.AddPooledTcpPrinter(null!, _ => configureCalled = true);
        var actNullConfigure = () => services.AddPooledTcpPrinter(null!);

        actNullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configureCalled.Should().BeFalse();

        actNullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void AddPooledTcpPrinter_ValidRegistration_RegistersSingletonAndFactoryResolvesPooledClient()
    {
        ServiceDescriptor? capturedDescriptor = null;
        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci => capturedDescriptor = ci.Arg<ServiceDescriptor>());

        services.AddPooledTcpPrinter(opt =>
        {
            opt.Host = "192.168.1.55";
            opt.Port = 9100;
            opt.NoDelay = true;
            opt.LingerSeconds = 2;
        });

        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.ServiceType.Should().Be<IPrinterClient>();
        capturedDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var sp = Substitute.For<IServiceProvider>();
        var client = capturedDescriptor.ImplementationFactory!(sp) as PooledTcpPrinterClient;
        client.Should().NotBeNull();
        client!.Options.Host.Should().Be("192.168.1.55");
        client.Options.Port.Should().Be(9100);
        client.Options.NoDelay.Should().BeTrue();
        client.Options.LingerSeconds.Should().Be(2);
    }

    [Fact]
    public void AddKeyedPooledTcpPrinter_NullArguments_ThrowsArgumentNullException()
    {
        var services = Substitute.For<IServiceCollection>();
        var configureCalled = false;

        var actNullServices = () => PrintingServiceCollectionExtensions.AddKeyedPooledTcpPrinter(null!, "key", _ => configureCalled = true);
        var actNullKey = () => services.AddKeyedPooledTcpPrinter(null, _ => configureCalled = true);
        var actNullConfigure = () => services.AddKeyedPooledTcpPrinter("key", null!);

        actNullServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configureCalled.Should().BeFalse();

        actNullKey.Should().Throw<ArgumentNullException>().WithParameterName("serviceKey");
        configureCalled.Should().BeFalse();

        actNullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void AddKeyedPooledTcpPrinter_ValidRegistration_RegistersKeyedSingleton()
    {
        ServiceDescriptor? capturedDescriptor = null;
        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci => capturedDescriptor = ci.Arg<ServiceDescriptor>());

        services.AddKeyedPooledTcpPrinter("receipt-printer", opt =>
        {
            opt.Host = "10.0.0.10";
            opt.Port = 9100;
            opt.TimeoutMs = 2500;
        });

        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.ServiceType.Should().Be<IPrinterClient>();
        capturedDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        capturedDescriptor.ServiceKey.Should().Be("receipt-printer");

        var sp = Substitute.For<IServiceProvider>();
        var client = capturedDescriptor.KeyedImplementationFactory!(sp, "receipt-printer") as PooledTcpPrinterClient;
        client.Should().NotBeNull();
        client!.Options.Host.Should().Be("10.0.0.10");
        client.Options.Port.Should().Be(9100);
        client.Options.TimeoutMs.Should().Be(2500);
    }

    [Fact]
    public void IPrintDocument_DefaultInterfaceImplementations_ProvideSensibleDefaults()
    {
        IPrintDocument doc = new MinimalPrintDocument();
        doc.DocumentName.Should().Be("MinDoc");
        doc.IsEmpty.Should().BeFalse();
        doc.IsIdempotent.Should().BeFalse();
        doc.GetBytes().Should().Equal([0x01, 0x02]);
        doc.GetMemory().ToArray().Should().Equal([0x01, 0x02]);
    }

    [Fact]
    public async Task RawPrintDocument_WriteToAsync_NullStream_ThrowsArgumentNullException()
    {
        var doc = new RawPrintDocument([0x1B, 0x40]);
        var act = async () => await doc.WriteToAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class MinimalPrintDocument : IPrintDocument
    {
        public string DocumentName => "MinDoc";
        public byte[] GetBytes() => [0x01, 0x02];
        public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}



