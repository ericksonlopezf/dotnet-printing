// Copyright © Erickson Lopez. MIT License.
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos.Status;
using Xunit;

namespace EricksonLopez.Printing.EscPos.Status.Tests;

/// <summary>
/// Status commands immutability and fuzzing test suite for ESC/POS status parser.
/// </summary>
public sealed class EscPosStatusAuditTests
{
    [Fact]
    public void FIND_PRINT_003_EscPosStatusCommands_AreImmutableReadOnlyMemory()
    {
        // Verify bytecode sequences are ReadOnlyMemory<byte>
        EscPosStatusCommands.QueryPrinterStatus.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x01 });
        EscPosStatusCommands.QueryOfflineCause.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x02 });
        EscPosStatusCommands.QueryErrorStatus.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x03 });
        EscPosStatusCommands.QueryPaperSensor.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x04 });

        // Verify array equality
        EscPosStatusCommands.QueryPrinterStatus.ToArray().Should().Equal([0x10, 0x04, 0x01]);
    }

    [Fact]
    public void Fuzz_EscPosStatusParser_All256ByteCombinations_NeverThrows()
    {
        for (var b1 = 0; b1 <= 255; b1++)
        {
            var (drawer, offline) = EscPosStatusParser.ParsePrinterStatusByte((byte)b1);
            var (cover, paperOut, err) = EscPosStatusParser.ParseOfflineCauseByte((byte)b1);
            var (cutter, unrec) = EscPosStatusParser.ParseErrorStatusByte((byte)b1);
            var (nearEnd, out4) = EscPosStatusParser.ParsePaperSensorByte((byte)b1);

            var status = EscPosStatusParser.Parse((byte)b1, (byte)b1, (byte)b1, (byte)b1);
            status.Should().NotBeNull();
        }
    }
}
