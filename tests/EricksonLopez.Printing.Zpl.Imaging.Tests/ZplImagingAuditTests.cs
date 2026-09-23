// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using AwesomeAssertions;
using EricksonLopez.Printing.Zpl;
using EricksonLopez.Printing.Zpl.Imaging;
using Xunit;
using ZplConverter = EricksonLopez.Printing.Zpl.Imaging.MonochromeBitmapConverter;

namespace EricksonLopez.Printing.Zpl.Imaging.Tests;

/// <summary>
/// Hardening and boundary verification suite for ZPL II graphic field conversions and dithering.
/// </summary>
public sealed class ZplImagingAuditTests
{
    [Fact]
    public void FIND_PRINT_005_ZplImaging_OperatesIndependentlyWithZplDitherAlgorithm()
    {
        // Raw grayscale 8x1
        byte[] gray = [0, 0, 255, 255, 0, 0, 255, 255];
        var doc = new ZplBuilder()
            .Image(10, 20, 8, 1, gray, bytesPerPixel: 1, ZplDitherAlgorithm.Threshold)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO10,20^GFA,1,1,1,CC^FS");

        // Verify Zpl.Imaging MonochromeBitmapConverter
        var packed = ZplConverter.ConvertRgbToPacked1Bit(
            8, 1, gray, 1, ZplDitherAlgorithm.FloydSteinberg);
        packed.Length.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    public void FIND_PRINT_019_ZplGraphicFieldConverter_ThrowsOnNonPositiveDimensions(int width, int height)
    {
        var data = new byte[100];
        var act = () => ZplGraphicFieldConverter.BuildGraphicFieldCommand(0, 0, width, height, data);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fuzz_ZplGraphicFieldConverter_PackedRasterMismatch_ThrowsArgumentException()
    {
        var act = () => ZplGraphicFieldConverter.BuildGraphicFieldCommand(0, 0, 100, 100, new byte[10]);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("packedRaster");
    }

    private static byte[] CreateValidBmp(int width = 2, int height = 2, short bitsPerPixel = 24, int pixelOffset = 54, int? totalSize = null)
    {
        var bytesPerPixel = Math.Max(1, (int)bitsPerPixel / 8);
        var safeWidth = Math.Max(1, Math.Abs(width));
        var safeHeight = Math.Max(1, Math.Abs(height));
        var rowStride = (safeWidth * bytesPerPixel + 3) & ~3;
        var dataSize = safeHeight * rowStride;
        var totalLength = totalSize ?? Math.Max(pixelOffset + dataSize, 120);

        var bmp = new byte[totalLength];
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        BitConverter.GetBytes(totalLength).CopyTo(bmp, 2);
        BitConverter.GetBytes(pixelOffset).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14); // Header size
        BitConverter.GetBytes(width).CopyTo(bmp, 18);
        BitConverter.GetBytes(height).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes(bitsPerPixel).CopyTo(bmp, 28);
        return bmp;
    }

    [Fact]
    public void ValidateBmp_TooSmall_ThrowsArgumentException()
    {
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(new byte[53]);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*header is too small.*")
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void ValidateBmp_InvalidMagicByte0_ThrowsArgumentException()
    {
        var bmp = CreateValidBmp();
        bmp[0] = 0x00;
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing 'BM' magic header.*")
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void ValidateBmp_InvalidMagicByte1_ThrowsArgumentException()
    {
        var bmp = CreateValidBmp();
        bmp[1] = 0x00;
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing 'BM' magic header.*")
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void ValidateBmp_RawHeightIntMinValue_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp();
        BitConverter.GetBytes(int.MinValue).CopyTo(bmp, 22);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*int.MinValue is not supported.*");
    }

    [Fact]
    public void ValidateBmp_PixelOffsetLessThan54_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(pixelOffset: 53);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*Invalid BMP pixel offset: 53.*");
    }

    [Fact]
    public void ValidateBmp_PixelOffsetExceedsLength_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp();
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 10);
        var act1 = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act1.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage($"*Invalid BMP pixel offset: {bmp.Length}.*");

        BitConverter.GetBytes(bmp.Length + 10).CopyTo(bmp, 10);
        var act2 = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act2.Should().Throw<System.IO.InvalidDataException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidateBmp_NonPositiveWidth_ThrowsInvalidDataException(int width)
    {
        var bmp = CreateValidBmp(width: width);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage($"*Unsupported BMP width: {width}.*");
    }

    [Fact]
    public void ValidateBmp_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(width: 8193);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*Unsupported BMP width: 8193. Must be between 1 and 8192.*");
    }

    [Fact]
    public void ValidateBmp_HeightZero_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(height: 0);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*Unsupported BMP height: 0.*");
    }

    [Theory]
    [InlineData(8193)]
    [InlineData(-8193)]
    public void ValidateBmp_HeightExceedsMaxDimension_ThrowsInvalidDataException(int rawHeight)
    {
        var bmp = CreateValidBmp(height: rawHeight);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*Unsupported BMP height: 8193. Must be between 1 and 8192.*");
    }

    [Theory]
    [InlineData((short)16)]
    [InlineData((short)8)]
    [InlineData((short)1)]
    public void ValidateBmp_UnsupportedBitsPerPixel_ThrowsNotSupportedException(short bpp)
    {
        var bmp = CreateValidBmp(bitsPerPixel: bpp);
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*Only 24-bit and 32-bit BMPs are supported. Found: {bpp}-bit.*");
    }

    [Fact]
    public void ValidateBmp_TruncatedPayload_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(totalSize: 69); // Needs 70 for 2x2 24bpp with offset 54
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*smaller than required image data*");
    }

    [Theory]
    [InlineData(ZplDitherAlgorithm.Threshold)]
    [InlineData(ZplDitherAlgorithm.FloydSteinberg)]
    public void ConvertBmp_TopDownBmp_ConvertsSuccessfully(ZplDitherAlgorithm algorithm)
    {
        var bmp = CreateValidBmp(width: 4, height: -2, bitsPerPixel: 24);
        var (w, h, packed) = ZplConverter.ConvertBmpToPacked1Bit(bmp, algorithm);
        w.Should().Be(4);
        h.Should().Be(2);
        packed.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(ZplDitherAlgorithm.Threshold)]
    [InlineData(ZplDitherAlgorithm.FloydSteinberg)]
    public void ConvertBmp_32BitBmp_ConvertsSuccessfully(ZplDitherAlgorithm algorithm)
    {
        var bmp = CreateValidBmp(width: 4, height: 2, bitsPerPixel: 32);
        var (w, h, packed) = ZplConverter.ConvertBmpToPacked1Bit(bmp, algorithm);
        w.Should().Be(4);
        h.Should().Be(2);
        packed.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(ZplDitherAlgorithm.Threshold)]
    [InlineData(ZplDitherAlgorithm.FloydSteinberg)]
    public void ConvertBmp_32BitBmp_TopDown_ConvertsSuccessfully(ZplDitherAlgorithm algorithm)
    {
        var bmp = CreateValidBmp(width: 4, height: -2, bitsPerPixel: 32);
        var (w, h, packed) = ZplConverter.ConvertBmpToPacked1Bit(bmp, algorithm);
        w.Should().Be(4);
        h.Should().Be(2);
        packed.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ConvertBmp_DifferentWidthRowStrides_ConvertsSuccessfully(int width)
    {
        var bmp = CreateValidBmp(width: width, height: 2, bitsPerPixel: 24);
        var (w, h, packed) = ZplConverter.ConvertBmpToPacked1Bit(bmp, ZplDitherAlgorithm.Threshold);
        w.Should().Be(width);
        h.Should().Be(2);
        packed.Length.Should().Be(((width + 7) / 8) * 2);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(8193, 10)]
    public void ConvertRgbToPacked1Bit_InvalidWidth_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        var act = () => ZplConverter.ConvertRgbToPacked1Bit(width, height, [0xFF], 1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(width))
            .WithMessage("*Width and height must be positive and not exceed*pixels*");
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 8193)]
    public void ConvertRgbToPacked1Bit_InvalidHeight_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        var act = () => ZplConverter.ConvertRgbToPacked1Bit(width, height, [0xFF], 1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(height))
            .WithMessage("*Width and height must be positive and not exceed*pixels*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(-1)]
    public void ConvertRgbToPacked1Bit_InvalidBytesPerPixel_ThrowsArgumentOutOfRangeException(int bpp)
    {
        var act = () => ZplConverter.ConvertRgbToPacked1Bit(10, 10, new byte[100], bpp);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("bytesPerPixel")
            .WithMessage("*Supported bytesPerPixel values are 1, 3, or 4.*");
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_PixelDataTooSmall_ThrowsArgumentException()
    {
        var act = () => ZplConverter.ConvertRgbToPacked1Bit(10, 10, new byte[10], 1);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("pixelData")
            .WithMessage("*smaller than required*");
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_CheckedOverflowProtection()
    {
        var act = () => ZplConverter.ConvertRgbToPacked1Bit(8192, 8192, new byte[10], 4);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("pixelData")
            .WithMessage("*smaller than required*");
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_AlgorithmsExecute()
    {
        byte[] gray = [0, 0, 255, 255, 0, 0, 255, 255];
        var thresh = ZplConverter.ConvertRgbToPacked1Bit(8, 1, gray, 1, ZplDitherAlgorithm.Threshold);
        thresh.Should().Equal([0xCC]);

        var floyd = ZplConverter.ConvertRgbToPacked1Bit(8, 1, gray, 1, ZplDitherAlgorithm.FloydSteinberg);
        floyd.Length.Should().Be(1);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_DitherAlgorithms_ProduceDifferentOutputs()
    {
        var gray = new byte[16];
        Array.Fill(gray, (byte)120);

        var threshold = ZplConverter.ConvertRgbToPacked1Bit(4, 4, gray, bytesPerPixel: 1, algorithm: ZplDitherAlgorithm.Threshold);
        var floyd = ZplConverter.ConvertRgbToPacked1Bit(4, 4, gray, bytesPerPixel: 1, algorithm: ZplDitherAlgorithm.FloydSteinberg);

        threshold.Should().Equal([0xF0, 0xF0, 0xF0, 0xF0]);
        threshold.Should().NotEqual(floyd);
        floyd.Should().NotEqual(new byte[floyd.Length]);
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_DitherAlgorithms_ProduceDifferentOutputs()
    {
        var bmp = CreateValidBmp(width: 4, height: 4, bitsPerPixel: 24);
        var pixelOffset = BitConverter.ToInt32(bmp.AsSpan(10, 4));
        var rowStride = (4 * 3 + 3) & ~3;
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var idx = pixelOffset + (y * rowStride) + (x * 3);
                bmp[idx] = 120;
                bmp[idx + 1] = 120;
                bmp[idx + 2] = 120;
            }
        }

        var (_, _, threshold) = ZplConverter.ConvertBmpToPacked1Bit(bmp, algorithm: ZplDitherAlgorithm.Threshold);
        var (_, _, floyd) = ZplConverter.ConvertBmpToPacked1Bit(bmp, algorithm: ZplDitherAlgorithm.FloydSteinberg);

        threshold.Should().Equal([0xF0, 0xF0, 0xF0, 0xF0]);
        threshold.Should().NotEqual(floyd);
        floyd.Should().NotEqual(new byte[floyd.Length]);
    }

    [Fact]
    public void ValidateBmp_LengthExactly54_DoesNotThrowHeaderTooSmall()
    {
        var bmp54 = new byte[54];
        bmp54[0] = 0x42;
        bmp54[1] = 0x4D;
        var act = () => ZplConverter.ConvertBmpToPacked1Bit(bmp54);
        act.Should().Throw<System.IO.InvalidDataException>();
    }

    [Fact]
    public void ValidateBmp_TopDownVsBottomUp_InvertsRowsCorrectly()
    {
        var bmpBottomUp = CreateValidBmp(width: 2, height: 2, bitsPerPixel: 24);
        var bmpTopDown = CreateValidBmp(width: 2, height: -2, bitsPerPixel: 24);

        var rowA = 54;
        bmpBottomUp[rowA] = 0; bmpBottomUp[rowA + 1] = 0; bmpBottomUp[rowA + 2] = 0;
        bmpBottomUp[rowA + 3] = 0; bmpBottomUp[rowA + 4] = 0; bmpBottomUp[rowA + 5] = 0;
        bmpTopDown[rowA] = 0; bmpTopDown[rowA + 1] = 0; bmpTopDown[rowA + 2] = 0;
        bmpTopDown[rowA + 3] = 0; bmpTopDown[rowA + 4] = 0; bmpTopDown[rowA + 5] = 0;

        var rowB = 54 + 8;
        bmpBottomUp[rowB] = 255; bmpBottomUp[rowB + 1] = 255; bmpBottomUp[rowB + 2] = 255;
        bmpBottomUp[rowB + 3] = 255; bmpBottomUp[rowB + 4] = 255; bmpBottomUp[rowB + 5] = 255;
        bmpTopDown[rowB] = 255; bmpTopDown[rowB + 1] = 255; bmpTopDown[rowB + 2] = 255;
        bmpTopDown[rowB + 3] = 255; bmpTopDown[rowB + 4] = 255; bmpTopDown[rowB + 5] = 255;

        var (_, _, packedBottomUp) = ZplConverter.ConvertBmpToPacked1Bit(bmpBottomUp, ZplDitherAlgorithm.Threshold);
        var (_, _, packedTopDown) = ZplConverter.ConvertBmpToPacked1Bit(bmpTopDown, ZplDitherAlgorithm.Threshold);

        packedBottomUp.Should().Equal([0x00, 0xC0]);
        packedTopDown.Should().Equal([0xC0, 0x00]);
    }
}
