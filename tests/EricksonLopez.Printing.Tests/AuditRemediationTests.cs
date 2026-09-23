// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Printing;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Tests;

/// <summary>
/// Exhaustive verification suite proving audit findings for the core Printing engine
/// have been remediated cleanly across all target frameworks.
/// </summary>
public sealed class AuditRemediationTests
{
    #region FIND-PRINT-008: ResilientPrinterClient Transmission Error Safety

    [Fact]
    public async Task FIND_PRINT_008_ResilientPrinterClient_DoesNotRetryOnTransmissionErrorByDefault()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "Receipt");

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Failure("Printer.TransmissionError", "Broken pipe"))));

        var client = new ResilientPrinterClient(inner, new ResilientPrinterOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryOnTransmissionError = false
        });

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.TransmissionError");
        // Inner client must NOT be retried to prevent duplicate physical printing
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FIND_PRINT_008_ResilientPrinterClient_RetriesOnTransmissionErrorWhenExplicitlyEnabled()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "Receipt");

        var calls = 0;
        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                if (calls < 3)
                {
                    return Task.FromResult(Result<bool>.Failure(Error.Failure("Printer.TransmissionError", "Network jitter")));
                }
                return Task.FromResult(Result<bool>.Success(true));
            });

        var client = new ResilientPrinterClient(inner, new ResilientPrinterOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryOnTransmissionError = true
        });

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        calls.Should().Be(3);
    }

    #endregion

    #region FIND-PRINT-010: DI Keyed Services Support

    [Fact]
    public void FIND_PRINT_010_PrintingServiceCollectionExtensions_AddKeyedPrinters_RegistersKeyedSingletons()
    {
        ServiceDescriptor? tcpDescriptor = null;

        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci =>
            {
                var desc = ci.Arg<ServiceDescriptor>();
                if (Equals(desc.ServiceKey, "Kitchen"))
                {
                    tcpDescriptor = desc;
                }
            });

        services.AddKeyedTcpPrinter("Kitchen", opt =>
        {
            opt.Host = "192.168.1.100";
            opt.Port = 9100;
        });

        tcpDescriptor.Should().NotBeNull();
        tcpDescriptor!.IsKeyedService.Should().BeTrue();
        tcpDescriptor.ServiceKey.Should().Be("Kitchen");
        tcpDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var sp = Substitute.For<IServiceProvider>();
        tcpDescriptor.KeyedImplementationFactory!(sp, "Kitchen").Should().BeOfType<TcpPrinterClient>();
    }

    #endregion

    #region FIND-PRINT-017: IPrintDocument Invariants

    [Fact]
    public void FIND_PRINT_017_IPrintDocument_GetMemory_ReturnsValidMemory()
    {
        byte[] raw = [0x1B, 0x40, 0x0A];
        IPrintDocument doc = new RawPrintDocument(raw, "TestMem", "key-123");

        doc.GetMemory().ToArray().Should().BeEquivalentTo(raw);
        doc.GetBytes().Should().BeEquivalentTo(raw);
        doc.IdempotencyKey.Should().Be("key-123");
    }

    #endregion
}
