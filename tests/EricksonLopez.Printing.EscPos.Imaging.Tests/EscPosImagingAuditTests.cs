// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos.Imaging;
using Xunit;
using EscPosConverter = EricksonLopez.Printing.EscPos.Imaging.MonochromeBitmapConverter;

namespace EricksonLopez.Printing.EscPos.Imaging.Tests;

/// <summary>
/// Hardening, fuzzing, and boundary safety test suite for the ESC/POS Imaging pipeline.
/// </summary>
public sealed class EscPosImagingAuditTests
{
    [Fact]
    public void FIND_PRINT_002_MonochromeBitmapConverter_EnforcesMaxDimension8192()
    {
        var actWidth = () => EscPosConverter.ConvertRgbToPacked1Bit(8193, 10, [0xFF], 1);
        actWidth.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("width")
            .WithMessage("*8192*");

        var actHeight = () => EscPosConverter.ConvertRgbToPacked1Bit(10, 8193, [0xFF], 1);
        actHeight.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("height")
            .WithMessage("*8192*");
    }

    [Fact]
    public void FIND_PRINT_002_MonochromeBitmapConverter_CheckedArithmeticPrevents64BitOverflow()
    {
        // Dimensions that would overflow 32-bit int if multiplied directly
        var act = () => EscPosConverter.ConvertRgbToPacked1Bit(8192, 8192, new byte[100], 4);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("pixelData")
            .WithMessage("*smaller than required*");
    }

    [Fact]
    public void FIND_PRINT_007_MonochromeBitmapConverter_IntMinValueHeight_ThrowsInvalidDataException()
    {
        var bmp = new byte[70];
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        BitConverter.GetBytes(54).CopyTo(bmp, 10); // pixelOffset
        BitConverter.GetBytes(40).CopyTo(bmp, 14); // biSize
        BitConverter.GetBytes(2).CopyTo(bmp, 18);  // width = 2
        BitConverter.GetBytes(int.MinValue).CopyTo(bmp, 22); // rawHeight = int.MinValue
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*int.MinValue*");
    }

    [Fact]
    public void FIND_PRINT_007_MonochromeBitmapConverter_InvalidPixelOffset_ThrowsInvalidDataException()
    {
        var bmp = new byte[70];
        bmp[0] = 0x42;
        bmp[1] = 0x4D;
        BitConverter.GetBytes(10).CopyTo(bmp, 10); // pixelOffset < 54
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(2).CopyTo(bmp, 18);
        BitConverter.GetBytes(2).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*pixel offset*");
    }

    [Fact]
    public void FIND_PRINT_007_MonochromeBitmapConverter_BuildEscPosRasterCommand_ThrowsWhenExceeding16Bit()
    {
        var actWidth = () => EscPosConverter.BuildEscPosRasterCommand(65536 * 8, 10, [0xFF]);
        actWidth.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("width")
            .WithMessage("*16-bit*");

        var actHeight = () => EscPosConverter.BuildEscPosRasterCommand(10, 65536, [0xFF]);
        actHeight.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("height")
            .WithMessage("*16-bit*");
    }

    [Fact]
    public void BuildEscPosRasterCommand_BoundaryValues65535_DoesNotThrow16BitLimit()
    {
        var rasterWidth = new byte[65535];
        var cmdWidth = EscPosConverter.BuildEscPosRasterCommand(65535 * 8, 1, rasterWidth);
        cmdWidth.Should().NotBeNull();
        cmdWidth[4].Should().Be(0xFF);
        cmdWidth[5].Should().Be(0xFF);

        var rasterHeight = new byte[65535];
        var cmdHeight = EscPosConverter.BuildEscPosRasterCommand(8, 65535, rasterHeight);
        cmdHeight.Should().NotBeNull();
        cmdHeight[6].Should().Be(0xFF);
        cmdHeight[7].Should().Be(0xFF);
    }

    [Fact]
    public void FIND_PRINT_016_SlidingWindowFloydSteinberg_MatchesExactDithering()
    {
        // 16x2 grayscale gradient image
        var gradient = new byte[32];
        for (var i = 0; i < 32; i++)
        {
            gradient[i] = (byte)(i * 8);
        }

        var packed = EscPosConverter.ConvertRgbToPacked1Bit(
            16, 2, gradient, bytesPerPixel: 1, EscPosDitherAlgorithm.FloydSteinberg);

        packed.Length.Should().Be(4); // (16 + 7)/8 * 2 = 4 bytes
        packed.Should().NotBeEquivalentTo(new byte[4]);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(255)]
    public void FIND_PRINT_018_MonochromeBitmapConverter_BuildEscPosRasterCommand_ThrowsOnScaleGreaterThan3(byte scale)
    {
        byte[] packed = [0xFF, 0x00];
        var act = () => EscPosConverter.BuildEscPosRasterCommand(8, 2, packed, scale);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(scale))
            .WithMessage("*Scale mode must be between 0 and 3.*");
    }

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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildEscPosRasterCommand_NonPositiveWidth_ThrowsArgumentOutOfRangeException(int width)
    {
        var act = () => EscPosConverter.BuildEscPosRasterCommand(width, 10, [0xFF]);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(width))
            .WithMessage("*Width must be positive.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildEscPosRasterCommand_NonPositiveHeight_ThrowsArgumentOutOfRangeException(int height)
    {
        var act = () => EscPosConverter.BuildEscPosRasterCommand(10, height, [0xFF]);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(height))
            .WithMessage("*Height must be positive.*");
    }

    [Fact]
    public void BuildEscPosRasterCommand_PackedRasterTooSmall_ThrowsArgumentException()
    {
        var act = () => EscPosConverter.BuildEscPosRasterCommand(16, 2, [0xFF]); // needs (16+7)/8 * 2 = 4 bytes
        act.Should().Throw<ArgumentException>()
            .WithParameterName("packedRaster")
            .WithMessage("*Packed raster data too small.*");
    }

    [Fact]
    public void BuildEscPosRasterCommand_ValidCommand_FormatsCorrectHeaderAndPayload()
    {
        byte[] raster = [0xAA, 0xBB];
        var cmd = EscPosConverter.BuildEscPosRasterCommand(8, 2, raster, scale: 2);
        cmd.Length.Should().Be(8 + 2);
        cmd[0].Should().Be(0x1D);
        cmd[1].Should().Be(0x76);
        cmd[2].Should().Be(0x30);
        cmd[3].Should().Be(2); // scale
        cmd[4].Should().Be(1); // xL ((8+7)/8 = 1)
        cmd[5].Should().Be(0); // xH
        cmd[6].Should().Be(2); // yL
        cmd[7].Should().Be(0); // yH
        cmd[8].Should().Be(0xAA);
        cmd[9].Should().Be(0xBB);
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
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(new byte[53]);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*header is too small.*")
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void ValidateBmp_InvalidMagicByte0_ThrowsArgumentException()
    {
        var bmp = CreateValidBmp();
        bmp[0] = 0x00;
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing 'BM' magic header.*")
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void ValidateBmp_InvalidMagicByte1_ThrowsArgumentException()
    {
        var bmp = CreateValidBmp();
        bmp[1] = 0x00;
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing 'BM' magic header.*")
            .WithParameterName("bmpBytes");
    }

    [Fact]
    public void ValidateBmp_PixelOffsetLessThan54_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(pixelOffset: 53);
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Invalid BMP pixel offset: 53.*");
    }

    [Fact]
    public void ValidateBmp_PixelOffsetExceedsLength_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp();
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 10);
        var act1 = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act1.Should().Throw<InvalidDataException>()
            .WithMessage($"*Invalid BMP pixel offset: {bmp.Length}.*");

        BitConverter.GetBytes(bmp.Length + 10).CopyTo(bmp, 10);
        var act2 = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act2.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidateBmp_NonPositiveWidth_ThrowsInvalidDataException(int width)
    {
        var bmp = CreateValidBmp(width: width);
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage($"*Unsupported BMP width: {width}.*");
    }

    [Fact]
    public void ValidateBmp_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(width: 8193);
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported BMP width: 8193. Must be between 1 and 8192.*");
    }

    [Fact]
    public void ValidateBmp_HeightZero_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(height: 0);
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported BMP height: 0.*");
    }

    [Theory]
    [InlineData(8193)]
    [InlineData(-8193)]
    public void ValidateBmp_HeightExceedsMaxDimension_ThrowsInvalidDataException(int rawHeight)
    {
        var bmp = CreateValidBmp(height: rawHeight);
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported BMP height: 8193. Must be between 1 and 8192.*");
    }

    [Theory]
    [InlineData(8192)]
    [InlineData(-8192)]
    public void ValidateBmp_HeightAtMaxDimension8192_DoesNotThrowUnsupportedHeight(int rawHeight)
    {
        var bmp = CreateValidBmp(width: 1, height: rawHeight);
        var (w, h, packed) = EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        w.Should().Be(1);
        h.Should().Be(8192);
        packed.Length.Should().Be(8192);
    }

    [Theory]
    [InlineData((short)16)]
    [InlineData((short)8)]
    [InlineData((short)1)]
    public void ValidateBmp_UnsupportedBitsPerPixel_ThrowsNotSupportedException(short bpp)
    {
        var bmp = CreateValidBmp(bitsPerPixel: bpp);
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*Only 24-bit and 32-bit BMPs are supported. Found: {bpp}-bit.*");
    }

    [Fact]
    public void ValidateBmp_TruncatedPayload_ThrowsInvalidDataException()
    {
        var bmp = CreateValidBmp(totalSize: 69); // Needs 70 for 2x2 24bpp with offset 54
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*smaller than required image data*");
    }

    [Theory]
    [InlineData(EscPosDitherAlgorithm.Threshold)]
    [InlineData(EscPosDitherAlgorithm.FloydSteinberg)]
    public void ConvertBmp_TopDownBmp_ConvertsSuccessfully(EscPosDitherAlgorithm algorithm)
    {
        var bmp = CreateValidBmp(width: 4, height: -2, bitsPerPixel: 24);
        var (w, h, packed) = EscPosConverter.ConvertBmpToPacked1Bit(bmp, algorithm);
        w.Should().Be(4);
        h.Should().Be(2);
        packed.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(EscPosDitherAlgorithm.Threshold)]
    [InlineData(EscPosDitherAlgorithm.FloydSteinberg)]
    public void ConvertBmp_32BitBmp_ConvertsSuccessfully(EscPosDitherAlgorithm algorithm)
    {
        var bmp = CreateValidBmp(width: 4, height: 2, bitsPerPixel: 32);
        var (w, h, packed) = EscPosConverter.ConvertBmpToPacked1Bit(bmp, algorithm);
        w.Should().Be(4);
        h.Should().Be(2);
        packed.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(EscPosDitherAlgorithm.Threshold)]
    [InlineData(EscPosDitherAlgorithm.FloydSteinberg)]
    public void ConvertBmp_32BitBmp_TopDown_ConvertsSuccessfully(EscPosDitherAlgorithm algorithm)
    {
        var bmp = CreateValidBmp(width: 4, height: -2, bitsPerPixel: 32);
        var (w, h, packed) = EscPosConverter.ConvertBmpToPacked1Bit(bmp, algorithm);
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
        var (w, h, packed) = EscPosConverter.ConvertBmpToPacked1Bit(bmp, EscPosDitherAlgorithm.Threshold);
        w.Should().Be(width);
        h.Should().Be(2);
        packed.Length.Should().Be(((width + 7) / 8) * 2);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_AlgorithmsExecute()
    {
        byte[] gray = [0, 0, 255, 255, 0, 0, 255, 255];
        var thresh = EscPosConverter.ConvertRgbToPacked1Bit(8, 1, gray, 1, EscPosDitherAlgorithm.Threshold);
        thresh.Should().Equal([0xCC]);

        var floyd = EscPosConverter.ConvertRgbToPacked1Bit(8, 1, gray, 1, EscPosDitherAlgorithm.FloydSteinberg);
        floyd.Length.Should().Be(1);
    }

    [Fact]
    public void ValidateBmp_LengthExactly54_DoesNotThrowHeaderTooSmall()
    {
        var bmp54 = new byte[54];
        bmp54[0] = 0x42;
        bmp54[1] = 0x4D;
        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp54);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_DitherAlgorithms_ProduceDifferentOutputs()
    {
        var gray = new byte[16];
        Array.Fill(gray, (byte)120);

        var threshold = EscPosConverter.ConvertRgbToPacked1Bit(4, 4, gray, bytesPerPixel: 1, algorithm: EscPosDitherAlgorithm.Threshold);
        var floyd = EscPosConverter.ConvertRgbToPacked1Bit(4, 4, gray, bytesPerPixel: 1, algorithm: EscPosDitherAlgorithm.FloydSteinberg);

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

        var (_, _, threshold) = EscPosConverter.ConvertBmpToPacked1Bit(bmp, algorithm: EscPosDitherAlgorithm.Threshold);
        var (_, _, floyd) = EscPosConverter.ConvertBmpToPacked1Bit(bmp, algorithm: EscPosDitherAlgorithm.FloydSteinberg);

        threshold.Should().Equal([0xF0, 0xF0, 0xF0, 0xF0]);
        threshold.Should().NotEqual(floyd);
        floyd.Should().NotEqual(new byte[floyd.Length]);
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

        var (_, _, packedBottomUp) = EscPosConverter.ConvertBmpToPacked1Bit(bmpBottomUp, EscPosDitherAlgorithm.Threshold);
        var (_, _, packedTopDown) = EscPosConverter.ConvertBmpToPacked1Bit(bmpTopDown, EscPosDitherAlgorithm.Threshold);

        packedBottomUp.Should().Equal([0x00, 0xC0]);
        packedTopDown.Should().Equal([0xC0, 0x00]);
    }
}
