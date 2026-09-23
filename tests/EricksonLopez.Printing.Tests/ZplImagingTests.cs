// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using AwesomeAssertions;
using EricksonLopez.Printing.Zpl;
using EricksonLopez.Printing.Zpl.Imaging;
using Xunit;

namespace EricksonLopez.Printing.Tests;

public sealed class ZplImagingTests
{
    [Fact]
    public void BuildGraphicFieldCommand_WithPackedRaster_ProducesValidGfHexCommand()
    {
        byte[] raster = [0xFF, 0x00, 0xAA, 0x55]; // 16 dots wide (2 bytes/row), 2 dots high => 4 bytes
        var gf = ZplGraphicFieldConverter.BuildGraphicFieldCommand(
            x: 50,
            y: 80,
            width: 16,
            height: 2,
            raster);

        gf.Should().StartWith("^FO50,80^GFA,4,4,2,FF00AA55");
        gf.TrimEnd().Should().EndWith("^FS");
    }

    [Fact]
    public void BuildGraphicFieldCommand_WhenPackedRasterTooShort_ThrowsArgumentException()
    {
        byte[] shortRaster = [0xFF, 0xAA]; // Only 2 bytes, needs 4
        var act = () => ZplGraphicFieldConverter.BuildGraphicFieldCommand(10, 20, 16, 2, shortRaster);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*smaller than required*")
            .WithParameterName("packedRaster");
    }

    [Fact]
    public void BuildGraphicFieldCommand_WhenRasterHasExtraBytes_TruncatesToRequiredLength()
    {
        byte[] extraRaster = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF]; // 6 bytes, but 8x2 only needs 2 bytes (1 byte/row * 2)
        var gf = ZplGraphicFieldConverter.BuildGraphicFieldCommand(0, 0, 8, 2, extraRaster);

        gf.Should().Contain("^GFA,2,2,1,AABB");
        gf.Should().NotContain("CC");
    }

    [Fact]
    public void GraphicField_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        var act = () => ZplImageBuilderExtensions.GraphicField(null!, 0, 0, 8, 1, [0xFF]);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("builder");
    }

    [Fact]
    public void GraphicField_AppendsDirectlyToZplBuilder()
    {
        var doc = new ZplBuilder()
            .GraphicField(30, 40, 8, 1, [0xA5])
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO30,40^GFA,1,1,1,A5^FS");
    }

    [Fact]
    public void Image_WithBmpBytes_ConvertsAndAppendsZplCommand()
    {
        var bmp = CreateTestBmp2x2();
        var doc = new ZplBuilder()
            .Image(15, 25, bmp, ZplDitherAlgorithm.Threshold)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        // 2x2 image: rowBytes = (2+7)/8 = 1, totalBytes = 2 * 1 = 2
        zpl.Should().Contain("^FO15,25^GFA,2,2,1,");
        zpl.Should().Contain("^FS");
    }

    [Fact]
    public void Image_WithBmpBytesAndFloydSteinberg_AppendsZplCommand()
    {
        var bmp = CreateTestBmp2x2();
        var doc = new ZplBuilder()
            .Image(10, 20, bmp, ZplDitherAlgorithm.FloydSteinberg)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO10,20^GFA,2,2,1,");
    }

    [Fact]
    public void Image_WithNullBuilderForBmp_ThrowsArgumentNullException()
    {
        var bmp = CreateTestBmp2x2();
        var act = () => ZplImageBuilderExtensions.Image(null!, 0, 0, bmp);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("builder");
    }

    [Fact]
    public void Image_WithNullBmpBytes_ThrowsArgumentNullException()
    {
        var builder = new ZplBuilder();
        var act = () => builder.Image(0, 0, (byte[])null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void Image_WithRawRgbPixels_ConvertsAndAppendsCommand()
    {
        // 8x2 pixels, 3 bytes per pixel (RGB)
        var rgbPixels = new byte[8 * 2 * 3];
        // Fill first 4 pixels of row 0 with black (0,0,0) and rest white
        for (var i = 0; i < 4 * 3; i++)
        {
            rgbPixels[i] = 0;
        }

        for (var i = 4 * 3; i < rgbPixels.Length; i++)
        {
            rgbPixels[i] = 255;
        }

        var doc = new ZplBuilder()
            .Image(5, 10, 8, 2, rgbPixels, bytesPerPixel: 3, ZplDitherAlgorithm.Threshold)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        // 8 dots wide: 1 byte per row. 2 rows: total 2 bytes.
        // Row 0 has 4 black pixels then 4 white => 0xF0
        // Row 1 has 8 white pixels => 0x00
        zpl.Should().Contain("^FO5,10^GFA,2,2,1,F000^FS");
    }

    [Fact]
    public void Image_WithRawGrayscalePixels_ConvertsAndAppendsCommand()
    {
        // 8x1 pixels, 1 byte per pixel (Grayscale)
        byte[] grayPixels = [0, 0, 255, 255, 0, 0, 255, 255]; // 0b11001100 = 0xCC

        var doc = new ZplBuilder()
            .Image(12, 34, 8, 1, grayPixels, bytesPerPixel: 1, ZplDitherAlgorithm.Threshold)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO12,34^GFA,1,1,1,CC^FS");
    }

    [Fact]
    public void Image_WithNullBuilderForPixelData_ThrowsArgumentNullException()
    {
        byte[] gray = [0];
        var act = () => ZplImageBuilderExtensions.Image(null!, 0, 0, 1, 1, gray);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("builder");
    }

    [Fact]
    public void MonochromeBitmapConverter_MaxDimension_Constant_Is8192()
    {
        MonochromeBitmapConverter.MaxDimension.Should().Be(8192);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_CustomMaxDimension_EnforcedCorrectly()
    {
        var pixelData = new byte[105 * 10 * 3];
        var actExceed = () => MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            105, 10, pixelData, maxDimension: 100);
        actExceed.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("width");

        var actValid = () => MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            100, 10, pixelData.AsSpan(0, 100 * 10 * 3), maxDimension: 100);
        actValid.Should().NotThrow();
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_CustomMaxDimension_EnforcedCorrectly()
    {
        var bmp2x2 = CreateTestBmp2x2();
        var actExceed = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(
            bmp2x2, maxDimension: 1);
        actExceed.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*Unsupported BMP width: 2. Must be between 1 and 1.*");

        var actValid = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(
            bmp2x2, maxDimension: 2);
        actValid.Should().NotThrow();
    }

    private static byte[] CreateTestBmp2x2()
    {
        var bmp = new byte[70];
        // Magic 'BM'
        bmp[0] = 0x42;
        bmp[1] = 0x4D;
        BitConverter.GetBytes(70).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(2).CopyTo(bmp, 18);
        BitConverter.GetBytes(2).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        // Fill bottom row (black + white + 2 pad bytes)
        var row0 = 54;
        bmp[row0] = 0; bmp[row0 + 1] = 0; bmp[row0 + 2] = 0;
        bmp[row0 + 3] = 255; bmp[row0 + 4] = 255; bmp[row0 + 5] = 255;

        // Fill top row (white + black + 2 pad bytes)
        var row1 = 54 + 8;
        bmp[row1] = 255; bmp[row1 + 1] = 255; bmp[row1 + 2] = 255;
        bmp[row1 + 3] = 0; bmp[row1 + 4] = 0; bmp[row1 + 5] = 0;

        return bmp;
    }
}
