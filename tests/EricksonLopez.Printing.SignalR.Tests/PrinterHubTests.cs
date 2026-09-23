// Copyright © Erickson Lopez. MIT License.
namespace EricksonLopez.Printing.SignalR.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Printing.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

public sealed class PrinterHubTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterPrinter_NullOrWhitespace_ThrowsArgumentException(string? invalidName)
    {
        var hub = new PrinterHub();

        var act = () => hub.RegisterPrinter(invalidName!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Printer name cannot be null or whitespace.*");
    }

    [Fact]
    public async Task RegisterPrinter_ValidName_EnrollsConnectionInNormalizedGroup()
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-42");

        var hub = new PrinterHub
        {
            Context = context,
            Groups = groups
        };

        await hub.RegisterPrinter("  Kitchen_Printer  ");

        await groups.Received(1).AddToGroupAsync("conn-42", "printer:KITCHEN_PRINTER", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnregisterPrinter_NullOrWhitespace_ReturnsEarlyWithoutGroupModification(string? invalidName)
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-42");

        var hub = new PrinterHub
        {
            Context = context,
            Groups = groups
        };

        await hub.UnregisterPrinter(invalidName!);

        await groups.DidNotReceiveWithAnyArgs().RemoveFromGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task UnregisterPrinter_ValidName_RemovesConnectionFromNormalizedGroup()
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-42");

        var hub = new PrinterHub
        {
            Context = context,
            Groups = groups
        };

        await hub.UnregisterPrinter("Receipt_01");

        await groups.Received(1).RemoveFromGroupAsync("conn-42", "printer:RECEIPT_01", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterPrinterAsync_ValidName_EnrollsConnectionInNormalizedGroup()
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-99");

        var hub = new PrinterHub
        {
            Context = context,
            Groups = groups
        };

        await hub.RegisterPrinterAsync("Bar_Printer");

        await groups.Received(1).AddToGroupAsync("conn-99", "printer:BAR_PRINTER", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnregisterPrinterAsync_ValidName_RemovesConnectionFromNormalizedGroup()
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-99");

        var hub = new PrinterHub
        {
            Context = context,
            Groups = groups
        };

        await hub.UnregisterPrinterAsync("Bar_Printer");

        await groups.Received(1).RemoveFromGroupAsync("conn-99", "printer:BAR_PRINTER", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetPrinterGroupName_NormalizesTrimsAndAppliesPrefix()
    {
        var groupName = PrinterHub.GetPrinterGroupName("  Thermal-FrontDesk  ");
        groupName.Should().Be("printer:THERMAL-FRONTDESK");
    }

    [Fact]
    public void AddPrintingSignalR_NullServices_ThrowsArgumentNullException()
    {
        var act = () => PrintingSignalRServiceCollectionExtensions.AddPrintingSignalR(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddPrintingSignalR_ValidRegistration_ResolvesHubPrinterDispatcher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrintingSignalR();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetService<IHubPrinterDispatcher>();

        dispatcher.Should().NotBeNull();
        dispatcher.Should().BeOfType<HubPrinterDispatcher>();
    }

    [Fact]
    public void PrintJobMessage_PropertiesSetAndRetrievedCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var msg = new PrintJobMessage(
            "job-101",
            "POS-01",
            "Invoice-99",
            "AQIDBA==",
            now);

        msg.JobId.Should().Be("job-101");
        msg.PrinterName.Should().Be("POS-01");
        msg.DocumentName.Should().Be("Invoice-99");
        msg.PayloadBase64.Should().Be("AQIDBA==");
        msg.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void HubPrinterDispatcher_ConstructorNullGuards_ThrowsArgumentNullException()
    {
        var act = () => new HubPrinterDispatcher(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("hubContext");
    }
}
