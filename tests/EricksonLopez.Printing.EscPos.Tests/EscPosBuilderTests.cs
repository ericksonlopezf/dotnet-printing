// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos.Tests;

using System;
using System.Text;
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos;
using Xunit;

public sealed class EscPosBuilderTests
{
    [Fact]
    public void Build_Receipt_ContainsExpectedEscPosByteSequence()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .Initialize()
            .Align(EscPosAlignment.Center)
            .Bold(true)
            .Line("MY STORE")
            .Bold(false)
            .Divider(42, '=')
            .TableRow("Item 1", "$10.00", 42, '.')
            .BarcodeCode128("INV123456", 64, true)
            .QrCode("https://ericksonlopez.dev/invoice/123")
            .OpenCashDrawer()
            .Cut()
            .Build("StoreReceipt");

        doc.Should().NotBeNull();
        doc.DocumentName.Should().Be("StoreReceipt");

        var bytes = doc.GetBytes();
        bytes.Should().NotBeEmpty();

        // 1. Check ESC @
        bytes[0].Should().Be(0x1B);
        bytes[1].Should().Be(0x40);

        // 2. Check Alignment (ESC a 1)
        bytes[2].Should().Be(0x1B);
        bytes[3].Should().Be(0x61);
        bytes[4].Should().Be(0x01);

        // 3. Check Cut (GS V 0) at end
        bytes[^3].Should().Be(0x1D);
        bytes[^2].Should().Be(0x56);
        bytes[^1].Should().Be(0x00);
    }

    [Fact]
    public void Alignment_AllVariants_EmitsCorrectOpcodes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .Align(EscPosAlignment.Left)
            .Align(EscPosAlignment.Center)
            .Align(EscPosAlignment.Right)
            .Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[]
        {
            0x1B, 0x61, 0x00, // Left
            0x1B, 0x61, 0x01, // Center
            0x1B, 0x61, 0x02  // Right
        });
    }

    [Fact]
    public void FontFormatting_BoldAndDoubleSize_EmitsCorrectOpcodes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .Bold(true)
            .Bold(false)
            .DoubleSize(doubleWidth: true, doubleHeight: false)
            .DoubleSize(doubleWidth: false, doubleHeight: true)
            .DoubleSize(doubleWidth: true, doubleHeight: true)
            .DoubleSize(doubleWidth: false, doubleHeight: false)
            .Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[]
        {
            0x1B, 0x45, 0x01, // Bold true
            0x1B, 0x45, 0x00, // Bold false
            0x1D, 0x21, 0x20, // Double width only
            0x1D, 0x21, 0x01, // Double height only
            0x1D, 0x21, 0x21, // Double width + height
            0x1D, 0x21, 0x00  // Normal size
        });
    }

    [Fact]
    public void TableRow_WhenFitsWithinWidth_FormatsSingleLineWithFillCharacter()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .TableRow("Left", "Right", totalWidth: 20, fillChar: '-')
            .Build();

        var text = Encoding.UTF8.GetString(doc.GetBytes());
        text.Should().Be("Left-----------Right\n");
    }

    [Fact]
    public void TableRow_WhenExceedsWidth_WrapsAcrossTwoLines()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .TableRow("VeryLongLeftColumnHeader", "$999,999.00", totalWidth: 20)
            .Build();

        var text = Encoding.UTF8.GetString(doc.GetBytes());
        text.Should().Be("VeryLongLeftColumnHeader\n         $999,999.00\n");
    }

    [Fact]
    public void Divider_ProducesSpecifiedWidthAndCharacter()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.Divider(10, '*').Build();

        var text = Encoding.UTF8.GetString(doc.GetBytes());
        text.Should().Be("**********\n");
    }

    [Fact]
    public void BarcodeCode128_ValidInput_EmitsBarcodeOpcodeSequence()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .BarcodeCode128("12345", height: 80, showHri: false)
            .Build();

        var bytes = doc.GetBytes();
        // GS H 0 (HRI disabled)
        bytes[0].Should().Be(0x1D);
        bytes[1].Should().Be(0x48);
        bytes[2].Should().Be(0x00);

        // GS h 80 (Height)
        bytes[3].Should().Be(0x1D);
        bytes[4].Should().Be(0x68);
        bytes[5].Should().Be(80);

        // GS k 73 5 '1' '2' '3' '4' '5'
        bytes[6].Should().Be(0x1D);
        bytes[7].Should().Be(0x6B);
        bytes[8].Should().Be(0x49);
        bytes[9].Should().Be(5);
        bytes[10].Should().Be((byte)'1');
    }

    [Fact]
    public void BarcodeCode128_NullOrWhitespace_ThrowsArgumentException()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var actNull = () => builder.BarcodeCode128(null!);
        var actEmpty = () => builder.BarcodeCode128("   ");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FeedAndCut_ProducesCorrectEscPosCommands()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .Feed(3)
            .Cut(partial: true)
            .Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[]
        {
            0x1B, 0x64, 0x03, // Feed 3 lines
            0x1D, 0x56, 0x01  // Partial cut
        });
    }

    [Fact]
    public void QrCode_AllErrorCorrectionLevels_EmitsCorrectOpcodes()
    {
        foreach (var ec in new[]
        {
            EscPosQrErrorCorrection.L,
            EscPosQrErrorCorrection.M,
            EscPosQrErrorCorrection.Q,
            EscPosQrErrorCorrection.H
        })
        {
            using var builder = new EscPosBuilder(safeMode: false);
            var doc = builder.QrCode("TEST", moduleSize: 4, errorCorrection: ec).Build();
            var bytes = doc.GetBytes();

            bytes.Should().NotBeEmpty();
            // Verify Error correction byte is present
            bytes.Should().Contain((byte)ec);
        }
    }

    [Fact]
    public void QrCode_NullData_ThrowsArgumentNullException()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var act = () => builder.QrCode(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("data");
    }

    [Fact]
    public void BarcodeCode128_Exceeding255Bytes_ThrowsArgumentException()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var longData = new string('A', 256);
        var act = () => builder.BarcodeCode128(longData);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*exceeds maximum allowed 255 bytes*");
    }

    [Fact]
    public void Raw_AppendsBytesDirectly()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        byte[] customCmd = [0x1B, 0x74, 0x02];
        var doc = builder.Raw(customCmd).Raw(customCmd.AsSpan()).Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[] { 0x1B, 0x74, 0x02, 0x1B, 0x74, 0x02 });
    }

    [Fact]
    public void Underline_EmitsCorrectOpcodes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .Underline(EscPosUnderline.SingleDot)
            .Underline(EscPosUnderline.DoubleDot)
            .Underline(EscPosUnderline.None)
            .Underline(enable: true, doubleThickness: false)
            .Underline(enable: true, doubleThickness: true)
            .Underline(enable: false)
            .Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[]
        {
            0x1B, 0x2D, 0x01, // SingleDot
            0x1B, 0x2D, 0x02, // DoubleDot
            0x1B, 0x2D, 0x00, // None
            0x1B, 0x2D, 0x01, // true, false
            0x1B, 0x2D, 0x02, // true, true
            0x1B, 0x2D, 0x00  // false
        });
    }

    [Fact]
    public void FontSize_EmitsCorrectOpcodes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .FontSize(1, 1) // 0x00
            .FontSize(2, 2) // ((2-1)<<4) | (2-1) = 0x11
            .FontSize(8, 8) // ((8-1)<<4) | (8-1) = 0x77
            .Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[]
        {
            0x1D, 0x21, 0x00,
            0x1D, 0x21, 0x11,
            0x1D, 0x21, 0x77
        });
    }

    [Fact]
    public void BarcodeCode39_ValidInput_EmitsBarcodeOpcodeSequence()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.BarcodeCode39("CODE39", height: 50, width: 3, showHri: true).Build();
        var bytes = doc.GetBytes();

        // Check GS w 3 (module width)
        bytes[0].Should().Be(0x1D);
        bytes[1].Should().Be(0x77);
        bytes[2].Should().Be(3);

        // Check GS H 2 (HRI below)
        bytes[3].Should().Be(0x1D);
        bytes[4].Should().Be(0x48);
        bytes[5].Should().Be(2);

        // Check GS h 50 (height)
        bytes[6].Should().Be(0x1D);
        bytes[7].Should().Be(0x68);
        bytes[8].Should().Be(50);

        // Check GS k 69 6 CODE39
        bytes[9].Should().Be(0x1D);
        bytes[10].Should().Be(0x6B);
        bytes[11].Should().Be(0x45); // 69 (0x45)
        bytes[12].Should().Be(6); // length
        Encoding.UTF8.GetString(bytes[13..19]).Should().Be("CODE39");
    }

    [Fact]
    public void BarcodeEan13_With12Digits_CalculatesChecksumAndEmits()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        // 400638133393 is a valid 12-digit prefix. Checksum for 400638133393:
        // sum = 4*1 + 0*3 + 0*1 + 6*3 + 3*1 + 8*3 + 1*1 + 3*3 + 3*1 + 3*3 + 9*1 + 3*3 = 4 + 18 + 3 + 24 + 1 + 9 + 3 + 9 + 9 + 9 = 89
        // check = (10 - (89 % 10)) % 10 = (10 - 9) % 10 = 1. Complete: 4006381333931
        var doc = builder.BarcodeEan13("400638133393").Build();
        var bytes = doc.GetBytes();

        bytes[9].Should().Be(0x1D);
        bytes[10].Should().Be(0x6B);
        bytes[11].Should().Be(0x43); // EAN-13
        bytes[12].Should().Be(13); // length
        Encoding.UTF8.GetString(bytes[13..26]).Should().Be("4006381333931");
    }

    [Fact]
    public void BarcodeEan13_WithInvalidCheckDigit_ThrowsArgumentException()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var act = () => builder.BarcodeEan13("4006381333939"); // Wrong check digit 9 instead of 1
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid EAN-13 check digit*");
    }

    [Fact]
    public void IPrintDocument_GetMemory_ReturnsIdenticalSequenceToGetBytes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.Line("Memory Test").Build();
        var bytes = doc.GetBytes();
        var memory = doc.GetMemory();

        memory.ToArray().Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void Dispose_MultipleInvocations_IsIdempotent()
    {
        var builder = new EscPosBuilder();
        builder.Initialize();
        builder.Dispose();
        var act = () => builder.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_CustomEncoding_UsesSpecifiedEncoding()
    {
        using var builder = new EscPosBuilder(Encoding.Latin1);
        var doc = builder.Text("café").Build();
        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(Encoding.Latin1.GetBytes("café"));
    }

    [Fact]
    public void Raw_NullByteArray_ThrowsArgumentNullException()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var act = () => builder.Raw((byte[])null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Raw_EmptySpan_DoesNotAppendBytes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        builder.Raw(ReadOnlySpan<byte>.Empty);
        var doc = builder.Build();
        doc.GetBytes().Should().BeEmpty();
    }

    [Fact]
    public void TableRow_NullColumns_TreatedAsEmptyStrings()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.TableRow(null!, "Price", 10, '-').Build();
        var text = Encoding.UTF8.GetString(doc.GetBytes());
        text.Should().Be("-----Price\n");

        using var builder2 = new EscPosBuilder();
        var doc2 = builder2.TableRow("Item", null!, 10, '-').Build();
        var text2 = Encoding.UTF8.GetString(doc2.GetBytes());
        text2.Should().Be("Item------\n");
    }

    [Fact]
    public void TableRow_WhenAvailableWidthIsExactlyZero_WrapsAcrossTwoLines()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        // left (5 chars) + right (5 chars) = 10 == totalWidth (10) => available = 0
        var doc = builder.TableRow("12345", "67890", totalWidth: 10).Build();
        var text = Encoding.UTF8.GetString(doc.GetBytes());
        text.Should().Be("12345\n     67890\n");
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public void BarcodeCode128_ShowHriToggle_EmitsExpectedOpcode(bool showHri, byte expectedHriByte)
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.BarcodeCode128("TEST", height: 64, showHri: showHri).Build();
        var bytes = doc.GetBytes();
        // GS H n
        bytes[0].Should().Be(0x1D);
        bytes[1].Should().Be(0x48);
        bytes[2].Should().Be(expectedHriByte);
    }

    [Fact]
    public void BarcodeCode128_BoundaryLengths_HandledCorrectly()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var maxData = new string('A', 255);
        var doc = builder.BarcodeCode128(maxData).Build();
        var bytes = doc.GetBytes();
        bytes[9].Should().Be(255); // Length byte

        var actExceed = () => builder.BarcodeCode128(new string('A', 256));
        actExceed.Should().Throw<ArgumentException>()
            .WithMessage("*exceeds maximum allowed 255 bytes*");
    }

    [Fact]
    public void BarcodeCode39_NullOrWhitespace_ThrowsArgumentException()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var actNull = () => builder.BarcodeCode39(null!);
        var actEmpty = () => builder.BarcodeCode39("   ");
        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public void BarcodeCode39_ShowHriToggle_EmitsExpectedOpcode(bool showHri, byte expectedHriByte)
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.BarcodeCode39("CODE", height: 64, width: 2, showHri: showHri).Build();
        var bytes = doc.GetBytes();
        // GS w n (bytes 0-2), GS H n (bytes 3-5)
        bytes[3].Should().Be(0x1D);
        bytes[4].Should().Be(0x48);
        bytes[5].Should().Be(expectedHriByte);
    }

    [Fact]
    public void BarcodeCode39_BoundaryLengths_HandledCorrectly()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var maxData = new string('A', 255);
        var doc = builder.BarcodeCode39(maxData).Build();
        var bytes = doc.GetBytes();
        bytes[12].Should().Be(255); // Length byte

        var actExceed = () => builder.BarcodeCode39(new string('A', 256));
        actExceed.Should().Throw<ArgumentException>()
            .WithMessage("*exceeds maximum allowed 255 bytes*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BarcodeEan13_NullOrWhitespace_ThrowsArgumentException(string? invalidData)
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var act = () => builder.BarcodeEan13(invalidData!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("12345")]             // Too short
    [InlineData("12345678901234")]    // Too long (14 digits)
    [InlineData("12345678901A")]      // 12 chars but contains letter
    [InlineData("1234567890/1")]      // Character < '0' ('/')
    [InlineData("1234567890:1")]      // Character > '9' (':')
    public void BarcodeEan13_InvalidFormatOrNonNumeric_ThrowsArgumentException(string invalidInput)
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var act = () => builder.BarcodeEan13(invalidInput);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must contain exactly 12 or 13 numeric digits*");
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public void BarcodeEan13_ShowHriToggle_EmitsExpectedOpcode(bool showHri, byte expectedHriByte)
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.BarcodeEan13("400638133393", showHri: showHri).Build();
        var bytes = doc.GetBytes();
        // GS w n (0-2), GS H n (3-5)
        bytes[3].Should().Be(0x1D);
        bytes[4].Should().Be(0x48);
        bytes[5].Should().Be(expectedHriByte);
    }

    [Fact]
    public void BarcodeEan13_ChecksumCalculation_EvenAndOddWeightsVerified()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        // 000000000000 -> sum = 0 -> check digit = 0 -> "0000000000000"
        var doc = builder.BarcodeEan13("000000000000").Build();
        var bytes = doc.GetBytes();
        Encoding.UTF8.GetString(bytes[13..26]).Should().Be("0000000000000");

        // 100000000000 -> pos 0 (even) digit 1: sum = 1 -> check digit = (10-1)%10 = 9 -> "1000000000009"
        using var builder2 = new EscPosBuilder();
        var doc2 = builder2.BarcodeEan13("100000000000").Build();
        var bytes2 = doc2.GetBytes();
        Encoding.UTF8.GetString(bytes2[13..26]).Should().Be("1000000000009");

        // 010000000000 -> pos 1 (odd) digit 1: sum = 3 -> check digit = (10-3)%10 = 7 -> "0100000000007"
        using var builder3 = new EscPosBuilder();
        var doc3 = builder3.BarcodeEan13("010000000000").Build();
        var bytes3 = doc3.GetBytes();
        Encoding.UTF8.GetString(bytes3[13..26]).Should().Be("0100000000007");
    }

    [Fact]
    public void OpenCashDrawer_EmitsExactPulseOpcodeSequence()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.OpenCashDrawer().Build();
        doc.GetBytes().Should().BeEquivalentTo(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA });
    }

    [Fact]
    public void QrCode_EmitsExactModelAndStoreOpcodes()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder.QrCode("ABC", moduleSize: 5, errorCorrection: EscPosQrErrorCorrection.M).Build();
        var bytes = doc.GetBytes();

        // 1. Model 2 setup: 1D 28 6B 04 00 31 41 32 00 (9 bytes)
        bytes[..9].Should().BeEquivalentTo(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });

        // 2. Module size: 1D 28 6B 03 00 31 43 05 (8 bytes)
        bytes[9..17].Should().BeEquivalentTo(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05 });

        // 3. Error correction M (0x31): 1D 28 6B 03 00 31 45 31 (8 bytes)
        bytes[17..25].Should().BeEquivalentTo(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31 });

        // 4. Store data: length = 3 + 3 = 6 => pL=6, pH=0 => 1D 28 6B 06 00 31 50 30 'A' 'B' 'C' (11 bytes)
        bytes[25..33].Should().BeEquivalentTo(new byte[] { 0x1D, 0x28, 0x6B, 0x06, 0x00, 0x31, 0x50, 0x30 });
        bytes[33..36].Should().BeEquivalentTo(new byte[] { (byte)'A', (byte)'B', (byte)'C' });

        // 5. Print: 1D 28 6B 03 00 31 51 30 (8 bytes)
        bytes[36..44].Should().BeEquivalentTo(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });
    }

    [Fact]
    public void QrCode_LargePayloadExceeding255Bytes_CalculatesHighByteCorrectly()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var data = new string('Z', 300);
        var doc = builder.QrCode(data).Build();
        var bytes = doc.GetBytes();

        // Length = 300 + 3 = 303 => pL = 303 & 0xFF = 47, pH = (303 >> 8) & 0xFF = 1
        // Store data header starts at index 9 + 8 + 8 = 25
        bytes[28].Should().Be(47); // pL
        bytes[29].Should().Be(1);  // pH
    }

    [Fact]
    public void Dispose_DisposesInternalBuffer_OperationsThrowObjectDisposedException()
    {
        var builder = new EscPosBuilder();
        builder.Dispose();

        var act = () => builder.Initialize();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void FontSize_ValuesOutsideAllowedRange_AreClampedToMinAndMax()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var doc = builder
            .FontSize(0, 9) // Clamped to (1, 8) => ((1-1)<<4) | (8-1) = 0x07
            .Build();

        var bytes = doc.GetBytes();
        bytes.Should().BeEquivalentTo(new byte[] { 0x1D, 0x21, 0x07 });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Text_NullOrEmpty_DoesNotAppend(string? input)
    {
        using var builder = new EscPosBuilder(safeMode: false);
        builder.Text(input!);
        var doc = builder.Build();
        doc.GetBytes().Should().BeEmpty();
    }
}



