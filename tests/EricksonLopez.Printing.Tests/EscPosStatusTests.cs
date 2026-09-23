// Copyright © Erickson Lopez. MIT License.
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos.Status;
using Xunit;

namespace EricksonLopez.Printing.Tests;

public sealed class EscPosStatusTests
{
    [Fact]
    public void EscPosStatusCommands_BytecodeConstants_MatchStandardEscPosProtocolSpecification()
    {
        EscPosStatusCommands.QueryPrinterStatus.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x01 });
        EscPosStatusCommands.QueryOfflineCause.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x02 });
        EscPosStatusCommands.QueryErrorStatus.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x03 });
        EscPosStatusCommands.QueryPaperSensor.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x04 });
    }

    [Theory]
    [InlineData(0x00, false, false)]
    [InlineData(0x04, true, false)]
    [InlineData(0x08, false, true)]
    [InlineData(0x0C, true, true)]
    [InlineData(0xF3, false, false)]
    public void ParsePrinterStatusByte_BitVariations_CorrectlyIdentifiesDrawerAndOffline(
        byte responseByte,
        bool expectedDrawerOpen,
        bool expectedOffline)
    {
        var (drawerOpen, isOffline) = EscPosStatusParser.ParsePrinterStatusByte(responseByte);

        drawerOpen.Should().Be(expectedDrawerOpen);
        isOffline.Should().Be(expectedOffline);
    }

    [Theory]
    [InlineData(0x00, false, false, false)]
    [InlineData(0x04, true, false, false)]
    [InlineData(0x20, false, true, false)]
    [InlineData(0x40, false, false, true)]
    [InlineData(0x64, true, true, true)]
    [InlineData(0x18, false, false, false)]
    public void ParseOfflineCauseByte_BitVariations_CorrectlyIdentifiesCoverPaperAndError(
        byte responseByte,
        bool expectedCoverOpen,
        bool expectedPaperOut,
        bool expectedHasError)
    {
        var (coverOpen, paperOut, hasError) = EscPosStatusParser.ParseOfflineCauseByte(responseByte);

        coverOpen.Should().Be(expectedCoverOpen);
        paperOut.Should().Be(expectedPaperOut);
        hasError.Should().Be(expectedHasError);
    }

    [Theory]
    [InlineData(0x00, false, false)]
    [InlineData(0x08, true, false)]
    [InlineData(0x20, false, true)]
    [InlineData(0x28, true, true)]
    [InlineData(0xD7, false, false)]
    public void ParseErrorStatusByte_BitVariations_CorrectlyIdentifiesCutterAndUnrecoverable(
        byte responseByte,
        bool expectedCutterError,
        bool expectedUnrecoverable)
    {
        var (cutterError, unrecoverable) = EscPosStatusParser.ParseErrorStatusByte(responseByte);

        cutterError.Should().Be(expectedCutterError);
        unrecoverable.Should().Be(expectedUnrecoverable);
    }

    [Theory]
    [InlineData(0x00, false, false)]
    [InlineData(0x04, false, false)] // Bit 2 only (needs both 2 and 3)
    [InlineData(0x08, false, false)] // Bit 3 only (needs both 2 and 3)
    [InlineData(0x0C, true, false)]  // Bits 2 & 3 set
    [InlineData(0x20, false, false)] // Bit 5 only (needs both 5 and 6)
    [InlineData(0x40, false, false)] // Bit 6 only (needs both 5 and 6)
    [InlineData(0x60, false, true)]  // Bits 5 & 6 set
    [InlineData(0x6C, true, true)]   // All sensor bits set
    [InlineData(0x93, false, false)] // Other unrelated bits
    public void ParsePaperSensorByte_BitVariations_RequiresBothBitsForNearEndAndPaperOut(
        byte responseByte,
        bool expectedNearEnd,
        bool expectedPaperOut)
    {
        var (nearEnd, paperOut) = EscPosStatusParser.ParsePaperSensorByte(responseByte);

        nearEnd.Should().Be(expectedNearEnd);
        paperOut.Should().Be(expectedPaperOut);
    }

    [Fact]
    public void Parse_HealthyPrinter_ReturnsOnlineWithNoErrors()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x00, 0x00, 0x00);

        status.IsOnline.Should().BeTrue();
        status.IsCoverOpen.Should().BeFalse();
        status.IsPaperOut.Should().BeFalse();
        status.IsPaperNearEnd.Should().BeFalse();
        status.IsDrawerOpen.Should().BeFalse();
        status.HasError.Should().BeFalse();
        status.HasCutterError.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenDrawerOpenAndOnline_MarksDrawerOpenWithoutAffectingOnline()
    {
        var status = EscPosStatusParser.Parse(0x04, 0x00, 0x00, 0x00);

        status.IsDrawerOpen.Should().BeTrue();
        status.IsOnline.Should().BeTrue();
        status.HasError.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenOfflineBitSetInByte1_MarksOffline()
    {
        var status = EscPosStatusParser.Parse(0x08, 0x00, 0x00, 0x00);

        status.IsOnline.Should().BeFalse();
        status.HasError.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenCoverOpenInByte2_MarksCoverOpenAndOffline()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x04, 0x00, 0x00);

        status.IsCoverOpen.Should().BeTrue();
        status.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenPaperOutInByte2_MarksPaperOutAndOffline()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x20, 0x00, 0x00);

        status.IsPaperOut.Should().BeTrue();
        status.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenPaperOutInByte4_MarksPaperOutAndOffline()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x00, 0x00, 0x60);

        status.IsPaperOut.Should().BeTrue();
        status.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenPaperNearEndInByte4_LeavesPrinterOnlineWithoutError()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x00, 0x00, 0x0C);

        status.IsPaperNearEnd.Should().BeTrue();
        status.IsPaperOut.Should().BeFalse();
        status.IsOnline.Should().BeTrue();
        status.HasError.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenOfflineCauseErrorInByte2_MarksHasErrorAndOffline()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x40, 0x00, 0x00);

        status.HasError.Should().BeTrue();
        status.HasCutterError.Should().BeFalse();
        status.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenCutterErrorInByte3_MarksCutterErrorAndHasErrorAndOffline()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x00, 0x08, 0x00);

        status.HasCutterError.Should().BeTrue();
        status.HasError.Should().BeTrue();
        status.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenUnrecoverableErrorInByte3_MarksHasErrorAndOfflineWithoutCutterError()
    {
        var status = EscPosStatusParser.Parse(0x00, 0x00, 0x20, 0x00);

        status.HasCutterError.Should().BeFalse();
        status.HasError.Should().BeTrue();
        status.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void Parse_CompositeErrorState_MarksAllRelevantFlagsCorrectly()
    {
        var status = EscPosStatusParser.Parse(0x08 | 0x04, 0x04 | 0x20 | 0x40, 0x08 | 0x20, 0x60 | 0x0C);

        status.IsOnline.Should().BeFalse();
        status.IsDrawerOpen.Should().BeTrue();
        status.IsCoverOpen.Should().BeTrue();
        status.IsPaperOut.Should().BeTrue();
        status.IsPaperNearEnd.Should().BeTrue();
        status.HasError.Should().BeTrue();
        status.HasCutterError.Should().BeTrue();
    }
}
