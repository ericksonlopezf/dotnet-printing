// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.EscPos.Imaging;
using EricksonLopez.Printing.EscPos.Status;
using EricksonLopez.Printing.Zpl;
using EricksonLopez.Printing.Zpl.Imaging;
using Xunit;
using EscPosConverter = EricksonLopez.Printing.EscPos.Imaging.MonochromeBitmapConverter;
using ZplConverter = EricksonLopez.Printing.Zpl.Imaging.MonochromeBitmapConverter;

namespace EricksonLopez.Printing.Tests;

/// <summary>
/// Adversarial Fuzzing and Red-Team verification suite targeting boundary conditions,
/// malformed headers, extreme inputs, and parser crash vectors.
/// </summary>
public sealed class AuditFuzzAndSecurityTests
{
    [Fact]
    public void Fuzz_BmpHeader_RandomBytes_NeverCausesUnhandledCrash()
    {
        var rng = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            var len = rng.Next(1, 200);
            var buffer = new byte[len];
            rng.NextBytes(buffer);

            // Parser must either succeed or throw ArgumentException/InvalidDataException/NotSupportedException.
            // It must NEVER throw NullReferenceException, IndexOutOfRangeException, or AccessViolationException.
            var act = () => EscPosConverter.ConvertBmpToPacked1Bit(buffer);
            try
            {
                act();
            }
            catch (Exception ex)
            {
                (ex is ArgumentException or InvalidDataException or NotSupportedException)
                    .Should().BeTrue($"Unexpected exception type: {ex.GetType().Name} for input length {len}");
            }
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    [InlineData(8193, 10)]
    [InlineData(10, 8193)]
    public void Fuzz_ConvertRgbToPacked1Bit_InvalidDimensions_ThrowsArgumentOutOfRangeException(int w, int h)
    {
        var act = () => EscPosConverter.ConvertRgbToPacked1Bit(w, h, [0xFF], 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(-1)]
    public void Fuzz_ConvertRgbToPacked1Bit_InvalidBytesPerPixel_ThrowsArgumentOutOfRangeException(int bpp)
    {
        var act = () => EscPosConverter.ConvertRgbToPacked1Bit(10, 10, new byte[100], bpp);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("bytesPerPixel");
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

    [Fact]
    public void Fuzz_EscPosBuilder_TableRow_UnicodeAndLargeStrings_DoesNotThrow()
    {
        using var builder = new EscPosBuilder();
        builder.TableRow("Café ☕", "€ 12.50", totalWidth: 42);
        builder.TableRow(new string('A', 100), new string('B', 100), totalWidth: 42);
        builder.TableRow("", "", totalWidth: 0);

        var doc = builder.Build();
        doc.GetBytes().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Fuzz_EscPosBuilder_QrCode_ExtremeInputs()
    {
        using var builder = new EscPosBuilder();
        var mediumUrl = "https://example.com/order?id=" + new string('9', 500);
        builder.QrCode(mediumUrl, moduleSize: 1, EscPosQrErrorCorrection.L);
        builder.QrCode(mediumUrl, moduleSize: 16, EscPosQrErrorCorrection.H);

        var doc = builder.Build();
        doc.GetBytes().Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public void Fuzz_ZplBuilder_EscapesComplexStrings_WithoutCorruption()
    {
        var malicious = "Test ^XA ^XZ ~SD25 _5E ^FO0,0^FDInjected^FS";
        var doc = new ZplBuilder()
            .Text(10, 10, malicious)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FH_");
        // Verify no raw unescaped ^XA or ~SD25 in the field data
        zpl.Should().NotContain("^FDTest ^XA");
    }

    [Theory]
    [InlineData("123^456")]
    [InlineData("123~456")]
    public void Fuzz_ZplBuilder_Barcodes_RejectCommandDelimiters(string barcode)
    {
        var b = new ZplBuilder();
        Assert.Throws<ArgumentException>(() => b.BarcodeCode128(0, 0, barcode));
        Assert.Throws<ArgumentException>(() => b.BarcodeCode39(0, 0, barcode));
        Assert.Throws<ArgumentException>(() => b.BarcodeEan13(0, 0, barcode));
    }

    [Fact]
    public void Fuzz_ZplGraphicFieldConverter_PackedRasterMismatch_ThrowsArgumentException()
    {
        var act = () => ZplGraphicFieldConverter.BuildGraphicFieldCommand(0, 0, 100, 100, new byte[10]);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("packedRaster");
    }
}
