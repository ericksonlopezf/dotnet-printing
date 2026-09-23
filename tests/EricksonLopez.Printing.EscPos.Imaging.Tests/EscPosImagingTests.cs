// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.EscPos.Imaging;
using Xunit;

namespace EricksonLopez.Printing.EscPos.Imaging.Tests;

public sealed class EscPosImagingTests
{
    [Fact]
    public void ConvertRgbToPacked1Bit_WithThreshold_ProducesExpectedDots()
    {
        // 8 pixels wide, 1 pixel high. First 4 pixels black (0,0,0), next 4 pixels white (255,255,255)
        var rgb = new byte[]
        {
            0, 0, 0,
            0, 0, 0,
            0, 0, 0,
            0, 0, 0,
            255, 255, 255,
            255, 255, 255,
            255, 255, 255,
            255, 255, 255
        };

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 8,
            height: 1,
            rgb,
            bytesPerPixel: 3,
            algorithm: EscPosDitherAlgorithm.Threshold);

        packed.Should().HaveCount(1);
        // Bit 7 to 4 are 1 (black), Bit 3 to 0 are 0 (white) => 0b11110000 = 0xF0
        packed[0].Should().Be(0xF0);
    }

    [Theory]
    [InlineData(0, 10, "width")]
    [InlineData(-5, 10, "width")]
    [InlineData(10, 0, "height")]
    [InlineData(10, -3, "height")]
    public void ConvertRgbToPacked1Bit_InvalidDimensions_ThrowsArgumentOutOfRangeException(int width, int height, string paramName)
    {
        byte[] buffer = [0, 0, 0];
        var act = () => MonochromeBitmapConverter.ConvertRgbToPacked1Bit(width, height, buffer);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Width and height must be positive and not exceed*pixels*")
            .WithParameterName(paramName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void ConvertRgbToPacked1Bit_UnsupportedBytesPerPixel_ThrowsArgumentOutOfRangeException(int bytesPerPixel)
    {
        byte[] buffer = new byte[100];
        var act = () => MonochromeBitmapConverter.ConvertRgbToPacked1Bit(10, 10, buffer, bytesPerPixel);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Supported bytesPerPixel values are 1, 3, or 4.*")
            .WithParameterName(nameof(bytesPerPixel));
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_DataTooShort_ThrowsArgumentException()
    {
        // 4x4 with 3 bytes per pixel requires 48 bytes; provide only 10 bytes
        byte[] tooShort = new byte[10];
        var act = () => MonochromeBitmapConverter.ConvertRgbToPacked1Bit(4, 4, tooShort, 3);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*smaller than required*")
            .WithParameterName("pixelData");
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_Grayscale_CalculatesLuminanceDirectly()
    {
        // 8 pixels grayscale: 4 black (30 < 128), 4 white (200 >= 128)
        byte[] gray = [30, 30, 30, 30, 200, 200, 200, 200];
        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(8, 1, gray, bytesPerPixel: 1, EscPosDitherAlgorithm.Threshold);

        packed.Should().HaveCount(1);
        packed[0].Should().Be(0xF0);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_Rgba_CalculatesLuminanceFromRgbIgnoringAlpha()
    {
        // 8 pixels RGBA: 4 white (255,255,255,255), 4 black (0,0,0,255)
        var rgba = new byte[8 * 4];
        for (var i = 0; i < 4; i++)
        {
            rgba[i * 4] = 255;
            rgba[i * 4 + 1] = 255;
            rgba[i * 4 + 2] = 255;
            rgba[i * 4 + 3] = 255;
        }
        for (var i = 4; i < 8; i++)
        {
            rgba[i * 4] = 0;
            rgba[i * 4 + 1] = 0;
            rgba[i * 4 + 2] = 0;
            rgba[i * 4 + 3] = 255;
        }

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(8, 1, rgba, bytesPerPixel: 4, EscPosDitherAlgorithm.Threshold);
        packed.Should().HaveCount(1);
        // First 4 are white (0), next 4 are black (1) => 0b00001111 = 0x0F
        packed[0].Should().Be(0x0F);
    }

    [Theory]
    [InlineData(255, 0, 0, 77, 0x80)]  // Red: lum = (255*299)/1000 = 76 < 77 => Black dot (1)
    [InlineData(255, 0, 0, 76, 0x00)]  // Red: lum = 76 not < 76 => White (0)
    [InlineData(0, 255, 0, 150, 0x80)] // Green: lum = (255*587)/1000 = 149 < 150 => Black dot (1)
    [InlineData(0, 255, 0, 149, 0x00)] // Green: lum = 149 not < 149 => White (0)
    [InlineData(0, 0, 255, 30, 0x80)]  // Blue: lum = (255*114)/1000 = 29 < 30 => Black dot (1)
    [InlineData(0, 0, 255, 29, 0x00)]  // Blue: lum = 29 not < 29 => White (0)
    public void ConvertRgbToPacked1Bit_LuminanceFormulaCoefficients_ExhaustivelyVerified(
        byte r, byte g, byte b, byte threshold, byte expectedByte)
    {
        // 8 pixels wide, 1 pixel high. First pixel has color (r,g,b), remaining 7 are white (255,255,255)
        var pixels = new byte[8 * 3];
        pixels[0] = r;
        pixels[1] = g;
        pixels[2] = b;
        for (var i = 3; i < pixels.Length; i++)
        {
            pixels[i] = 255;
        }

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 8,
            height: 1,
            pixels,
            bytesPerPixel: 3,
            algorithm: EscPosDitherAlgorithm.Threshold,
            threshold: threshold);

        packed[0].Should().Be(expectedByte);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_MultiRowAndMultiByteOffset_AddressesCorrectByteInThresholdMode()
    {
        // 16 pixels wide (widthInBytes = 2), 3 rows high (total 6 bytes)
        // All pixels white (255) except at x = 9, y = 2 which is black (0)
        var pixels = new byte[16 * 3];
        Array.Fill<byte>(pixels, 255);
        pixels[2 * 16 + 9] = 0; // Row 2, Column 9

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 16,
            height: 3,
            pixels,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.Threshold);

        packed.Should().HaveCount(6);
        // Row 0: bytes 0 and 1 should be 0
        packed[0].Should().Be(0);
        packed[1].Should().Be(0);
        // Row 1: bytes 2 and 3 should be 0
        packed[2].Should().Be(0);
        packed[3].Should().Be(0);
        // Row 2: byte 4 should be 0, byte 5 (column 8..15) has pixel 9 (bit 6) set => 0x40
        packed[4].Should().Be(0);
        packed[5].Should().Be(0x40);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_MultiRowAndMultiByteOffset_AddressesCorrectByteInFloydSteinbergMode()
    {
        // 16 pixels wide, 3 rows high
        // All pixels white (255) except at x = 9, y = 2 which is black (0)
        var pixels = new byte[16 * 3];
        Array.Fill<byte>(pixels, 255);
        pixels[2 * 16 + 9] = 0;

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 16,
            height: 3,
            pixels,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg);

        packed.Should().HaveCount(6);
        packed[0].Should().Be(0);
        packed[1].Should().Be(0);
        packed[2].Should().Be(0);
        packed[3].Should().Be(0);
        packed[4].Should().Be(0);
        packed[5].Should().Be(0x40);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_ExactThresholdBoundaryVerification()
    {
        // 1x1 image with oldPixel exactly equal to threshold (128)
        // Under strict '<': 128 < 128 is false => newPixel = 255 (white dot, bit 0)
        // Under '<=': 128 <= 128 is true => newPixel = 0 (black dot, bit 1)
        byte[] pixel = [128];
        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(1, 1, pixel, bytesPerPixel: 1, algorithm: EscPosDitherAlgorithm.FloydSteinberg, threshold: 128);

        packed[0].Should().Be(0x00);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_MathematicalPrecisionDiffusion()
    {
        // 2x2 image where all pixels have initial gray = 140, threshold = 128
        // Trace:
        // (0,0): 140 >= 128 => newPixel=255 (white dot=0), error = -115
        // Diffusion from (0,0):
        //   (1,0) gets (-115*7)/16 = -50 => gray[0,1] = 140 - 50 = 90
        //   (0,1) gets (-115*5)/16 = -35 => gray[1,0] = 140 - 35 = 105
        //   (1,1) gets (-115*1)/16 = -7  => gray[1,1] = 140 - 7 = 133
        // (1,0): 90 < 128 => newPixel=0 (black dot=1), error = +90
        // Diffusion from (1,0):
        //   (0,1) gets (+90*3)/16 = +16 => gray[1,0] = 105 + 16 = 121
        //   (1,1) gets (+90*5)/16 = +28 => gray[1,1] = 133 + 28 = 161
        // (0,1): 121 < 128 => newPixel=0 (black dot=1), error = +121
        // Diffusion from (0,1):
        //   (1,1) gets (+121*7)/16 = +52 => gray[1,1] = 161 + 52 = 213
        // (1,1): 213 >= 128 => newPixel=255 (white dot=0)
        // Expected bits:
        // Row 0: pixel(0,0)=0, pixel(1,0)=1 => bit 7=0, bit 6=1 => 0x40
        // Row 1: pixel(0,1)=1, pixel(1,1)=0 => bit 7=1, bit 6=0 => 0x80

        var grayPixels = new byte[]
        {
            140, 140,
            140, 140
        };

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 2,
            height: 2,
            grayPixels,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg,
            threshold: 128);

        packed.Should().HaveCount(2); // 1 byte per row * 2 rows
        packed[0].Should().Be(0x40);
        packed[1].Should().Be(0x80);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_DirectionalDiffusionsExhaustivelyTested()
    {
        // 3x2 image specifically designed so that diffusions across all 4 neighbor directions
        // (x+1, y), (x-1, y+1), (x, y+1), and (x+1, y+1) flip pixels across the 128 threshold.
        // Row 0: [255, 90, 255]
        // Row 1: [115, 110, 125]
        // Under correct Floyd-Steinberg:
        //   (0,1) receives +16 from (1,0) via (error * 3) / 16 -> 115 + 16 = 131 >= 128 (turns white!)
        //   Row 0 produces 0x40 (only pixel 1 is black)
        //   Row 1 produces 0x40 (only pixel 1 is black)
        byte[] pixels =
        [
            255, 90, 255,
            115, 110, 125
        ];

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 3,
            height: 2,
            pixels,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg,
            threshold: 128);

        packed.Should().HaveCount(2);
        packed[0].Should().Be(0x40);
        packed[1].Should().Be(0x40);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_DiagonalDiffusionTermPrecisionKillsMutants()
    {
        // 2x2 image calibrated to verify diagonal error term (error * 1) / 16 at (x+1, y+1)
        // Initial gray values:
        // (0, 0) = 112 -> error = 112 -> diffuses (112*1)/16 = +7 to (1,1)
        // (1, 0) = 0   -> gets (112*7)/16 = 49 -> error = 49 -> diffuses (49*5)/16 = +15 to (1,1)
        // (0, 1) = 0   -> gets (112*5)/16 + (49*3)/16 = 35 + 9 = 44 -> error = 44 -> diffuses (44*7)/16 = +19 to (1,1)
        // (1, 1) = 87  -> receives 7 + 15 + 19 = 41 -> 87 + 41 = 128 >= 128 -> turns white (0)!
        // If (error * 1) / 16 is subtracted or omitted, gray[1,1] <= 121 < 128 -> turns black (1), flipping bit 6 of row 1.
        byte[] gray =
        [
            112, 0,
            0, 87
        ];

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 2,
            height: 2,
            gray,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg,
            threshold: 128);

        packed.Should().HaveCount(2);
        // Row 0: pixel(0,0)=1, pixel(1,0)=1 => 0b11000000 = 0xC0
        // Row 1: pixel(0,1)=1, pixel(1,1)=0 => 0b10000000 = 0x80
        packed[0].Should().Be(0xC0);
        packed[1].Should().Be(0x80);
    }


    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_MultiRowMultiColumnPixelIndexing()
    {
        // 3x2 image with bytesPerPixel = 3 (RGB).
        // Set all pixels white (255) except at (x=2, y=1) which is black (0,0,0)
        // Index of (2,1) = (1 * 3 + 2) * 3 = 15.
        // If division were used instead of multiplication: (1 * 3 + 2) / 3 = 1 (wrong pixel).
        var pixels = new byte[3 * 2 * 3];
        Array.Fill<byte>(pixels, 255);
        pixels[15] = 0;
        pixels[16] = 0;
        pixels[17] = 0;

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 3,
            height: 2,
            pixels,
            bytesPerPixel: 3,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg,
            threshold: 128);

        packed.Should().HaveCount(2);
        // Row 0 should be all white => 0x00
        packed[0].Should().Be(0x00);
        // Row 1: pixel 2 is black => bit 5 is 1 => 0b00100000 = 0x20
        packed[1].Should().Be(0x20);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_DiagonalDiffusionKillsSubtractionAndConditionMutants()
    {
        // 2x2 image where:
        // (0,0) = 16 (< 128 => newPixel = 0, error = +16)
        // (1,0) = 248 (receives (+16*7)/16 = +7 => 255 => error = 255 - 255 = 0)
        // (0,1) = 250 (receives (+16*5)/16 = +5 => 255 => error = 255 - 255 = 0)
        // (1,1) = 127
        // (1,1) receives ONLY (+16*1)/16 = +1 from (0,0):
        //   Under correct code: 127 + 1 = 128 >= 128 => White dot (bit 6 = 0).
        //   Under '-=' mutant: 127 - 1 = 126 < 128 => Black dot (bit 6 = 1).
        //   Under 'x + 1 > width' mutant: 127 + 0 = 127 < 128 => Black dot (bit 6 = 1).
        byte[] pixels =
        [
            16, 248,
            250, 127
        ];

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 2,
            height: 2,
            pixels,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg,
            threshold: 128);

        // Row 1, bit 6 must be 0 (white dot)
        (packed[1] & 0x40).Should().Be(0x00);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_DiagonalDiffusionKillsDivisorMutant()
    {
        // 2x2 image where:
        // (0,0) = 239 (>= 128 => newPixel = 255, error = 239 - 255 = -16)
        // (1,0) = 0
        // (0,1) = 0
        // (1,1) = 135
        // Trace:
        //   Under correct code: (1,1) receives (-16*1)/16 = -1, plus -2 from (1,0) and -2 from (0,1) => total -5.
        //     gray[1,1] = 135 - 5 = 130 >= 128 => White dot (bit 6 = 0).
        //   Under 'error / 1' mutant: (1,1) receives -16, plus -2 and -2 => total -20.
        //     gray[1,1] = 135 - 20 = 115 < 128 => Black dot (bit 6 = 1).
        byte[] pixels =
        [
            239, 0,
            0, 135
        ];

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            width: 2,
            height: 2,
            pixels,
            bytesPerPixel: 1,
            algorithm: EscPosDitherAlgorithm.FloydSteinberg,
            threshold: 128);

        // Row 1, bit 6 must be 0 (white dot)
        (packed[1] & 0x40).Should().Be(0x00);
    }

    [Fact]
    public void ConvertRgbToPacked1Bit_FloydSteinberg_Width1AndHeight1EdgeCases()
    {
        // 1x1 image tests condition where no neighbors exist (x+1 < width is false, y+1 < height is false)
        byte[] singlePixel = [50];
        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(1, 1, singlePixel, bytesPerPixel: 1, algorithm: EscPosDitherAlgorithm.FloydSteinberg);

        packed.Should().HaveCount(1);
        packed[0].Should().Be(0x80); // Black dot at bit 7
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_HeaderTooSmall_ThrowsArgumentException()
    {
        byte[] tinyBmp = [0x42, 0x4D, 0x00];
        var act = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(tinyBmp);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*header is too small*");
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_InvalidMagicHeader_ThrowsArgumentException()
    {
        var bmp = new byte[60];
        bmp[0] = 0x5A; // 'Z'
        bmp[1] = 0x50; // 'P'
        var act = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing 'BM' magic header*");
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_FirstMagicByteValidSecondInvalid_ThrowsArgumentException()
    {
        var bmp = new byte[60];
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x00; // not 'M'
        var act = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing 'BM' magic header*");
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_InvalidWidth_ThrowsInvalidDataException()
    {
        var bmp = CreateTestBmp(width: 0, height: 2, bitsPerPixel: 24);
        var act = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported BMP width*");
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_UnsupportedBitsPerPixel_ThrowsNotSupportedException()
    {
        var bmp = CreateTestBmp(width: 2, height: 2, bitsPerPixel: 16);
        var act = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Only 24-bit and 32-bit BMPs are supported*");
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_TopDown24BitBmp_ExtractsPixelsInTopDownOrder()
    {
        // 2x2 BMP with negative height (-2) = top-down
        // In top-down: row 0 in BMP is top row on screen (row 0), row 1 is bottom row (row 1)
        var bmp = new byte[70];
        bmp[0] = 0x42; bmp[1] = 0x4D;
        BitConverter.GetBytes(70).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(2).CopyTo(bmp, 18);
        BitConverter.GetBytes(-2).CopyTo(bmp, 22); // Negative height = top-down
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        // Top row (offset 54): pixel 0 is Black (0,0,0), pixel 1 is White (255,255,255) + 2 pad bytes
        bmp[54] = 0; bmp[55] = 0; bmp[56] = 0;
        bmp[57] = 255; bmp[58] = 255; bmp[59] = 255;

        // Bottom row (offset 54 + 8): pixel 0 is White (255,255,255), pixel 1 is Black (0,0,0) + 2 pad bytes
        bmp[62] = 255; bmp[63] = 255; bmp[64] = 255;
        bmp[65] = 0; bmp[66] = 0; bmp[67] = 0;

        var (w, h, packed) = MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp, EscPosDitherAlgorithm.Threshold);
        w.Should().Be(2);
        h.Should().Be(2);
        packed.Should().HaveCount(2);

        // In top-down, row 0 has pixel 0 black, pixel 1 white => bit 7=1, bit 6=0 => 0x80
        packed[0].Should().Be(0x80);
        // Row 1 has pixel 0 white, pixel 1 black => bit 7=0, bit 6=1 => 0x40
        packed[1].Should().Be(0x40);
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_32BitBmp_UnpacksBgraPixelsCorrectly()
    {
        // 2x2 32-bit BMP (BGRA)
        var bmp = CreateTestBmp(width: 2, height: 2, bitsPerPixel: 32);

        var (w, h, packed) = MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp, EscPosDitherAlgorithm.Threshold);
        w.Should().Be(2);
        h.Should().Be(2);
        packed.Should().HaveCount(2);
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_StrictBgrChannelUnpacking_DistinguishesRedFromBlue()
    {
        // Create a 1x1 24-bit BMP with B=0, G=0, R=255 (Pure Red). Stride for 1 pixel is 4 bytes.
        var bmp = new byte[58];
        bmp[0] = 0x42;
        bmp[1] = 0x4D;
        BitConverter.GetBytes(58).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(1).CopyTo(bmp, 18);
        BitConverter.GetBytes(1).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        // Pixel at offset 54: B=0, G=0, R=255, Pad=0
        bmp[54] = 0;   // Blue
        bmp[55] = 0;   // Green
        bmp[56] = 255; // Red
        bmp[57] = 0;   // Pad

        // If B and R are swapped by mutator, Blue luminance is 29, Red is 76
        // With threshold = 50:
        // Pure Red lum = 76 >= 50 => White (bit=0)
        // If swapped to Blue lum = 29 < 50 => Black (bit=1)
        var (w, h, packed) = MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp, EscPosDitherAlgorithm.Threshold, threshold: 50);
        w.Should().Be(1);
        h.Should().Be(1);
        packed[0].Should().Be(0x00); // Must be White
    }

    [Fact]
    public void ConvertBmpToPacked1Bit_RowStridePaddingArithmetic_CorrectlySkipsPaddingBytes()
    {
        // 3 pixels wide 24-bit BMP: 3 * 3 = 9 bytes + 3 pad bytes = 12 bytes row stride
        // 2 rows: total size 54 + 24 = 78 bytes
        var bmp = new byte[78];
        bmp[0] = 0x42;
        bmp[1] = 0x4D;
        BitConverter.GetBytes(78).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(3).CopyTo(bmp, 18);
        BitConverter.GetBytes(2).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        // Fill row 0 (bottom row): 3 white pixels (9 bytes 255) + 3 pad bytes (0)
        for (var i = 0; i < 9; i++)
        {
            bmp[54 + i] = 255;
        }

        // Fill row 1 (top row): 3 black pixels (9 bytes 0) + 3 pad bytes (255)
        for (var i = 0; i < 9; i++)
        {
            bmp[54 + 12 + i] = 0;
        }

        var (w, h, packed) = MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmp, EscPosDitherAlgorithm.Threshold);
        w.Should().Be(3);
        h.Should().Be(2);
        packed.Should().HaveCount(2);

        // Top row (row 1 in BMP): 3 black pixels => bits 7, 6, 5 are 1 => 0b11100000 = 0xE0
        packed[0].Should().Be(0xE0);
        // Bottom row (row 0 in BMP): 3 white pixels => bits 7, 6, 5 are 0 => 0b00000000 = 0x00
        packed[1].Should().Be(0x00);
    }

    [Fact]
    public void BuildEscPosRasterCommand_ProducesValidGsV0Sequence()
    {
        byte[] packedData = [0xF0, 0x0F];
        // 16 dots wide (2 bytes wide), 1 dot high
        var cmd = MonochromeBitmapConverter.BuildEscPosRasterCommand(
            width: 16,
            height: 1,
            packedData,
            scale: 0);

        // Header: GS v 0 m xL xH yL yH
        cmd[0].Should().Be(0x1D);
        cmd[1].Should().Be(0x76);
        cmd[2].Should().Be(0x30);
        cmd[3].Should().Be(0x00); // normal scale
        cmd[4].Should().Be(2);    // xL (2 bytes)
        cmd[5].Should().Be(0);    // xH
        cmd[6].Should().Be(1);    // yL (1 dot high)
        cmd[7].Should().Be(0);    // yH
        cmd[8].Should().Be(0xF0);
        cmd[9].Should().Be(0x0F);
    }

    [Fact]
    public void BuildEscPosRasterCommand_WithCustomScaleAndLargeDimensions_EncodesHeaderCorrectly()
    {
        byte[] packed = new byte[400 * 50]; // 50 bytes wide (400 dots), 300 dots high
        var cmd = MonochromeBitmapConverter.BuildEscPosRasterCommand(
            width: 400,
            height: 300,
            packed,
            scale: 3); // Quadruple

        cmd[3].Should().Be(3);
        // widthInBytes = (400+7)/8 = 50 => xL=50, xH=0
        cmd[4].Should().Be(50);
        cmd[5].Should().Be(0);
        // height = 300 => yL=300&0xFF=44, yH=(300>>8)&0xFF=1
        cmd[6].Should().Be(44);
        cmd[7].Should().Be(1);
    }

    [Fact]
    public void BuildEscPosRasterCommand_WithHighByteInWidth_EncodesXHProperly()
    {
        // width = 2500 dots => widthInBytes = (2500+7)/8 = 313 bytes => xL = 313 & 0xFF = 57, xH = (313 >> 8) & 0xFF = 1
        byte[] packed = new byte[313];
        var cmd = MonochromeBitmapConverter.BuildEscPosRasterCommand(
            width: 2500,
            height: 1,
            packed,
            scale: 0);

        cmd[4].Should().Be(57);
        cmd[5].Should().Be(1);
    }

    [Theory]
    [InlineData(7, 1)]  // 7 dots wide => widthInBytes = 1
    [InlineData(8, 1)]  // 8 dots wide => widthInBytes = 1
    [InlineData(9, 2)]  // 9 dots wide => widthInBytes = 2
    [InlineData(16, 2)] // 16 dots wide => widthInBytes = 2
    [InlineData(17, 3)] // 17 dots wide => widthInBytes = 3
    public void BuildEscPosRasterCommand_WidthInBytesBoundary_CalculatedAccurately(int width, int expectedWidthInBytes)
    {
        byte[] data = new byte[expectedWidthInBytes];
        var cmd = MonochromeBitmapConverter.BuildEscPosRasterCommand(width, 1, data);

        cmd[4].Should().Be((byte)expectedWidthInBytes);
        cmd[5].Should().Be(0);
    }

    [Fact]
    public void EscPosBuilder_ImageExtensionWithBmp_AppendsRasterBitCommand()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var bmp = CreateTestBmp(width: 2, height: 2, bitsPerPixel: 24);
        builder.Image(bmp, EscPosDitherAlgorithm.Threshold, scale: 1);

        var doc = builder.Build();
        var bytes = doc.GetBytes();

        bytes.Should().StartWith(new byte[] { 0x1D, 0x76, 0x30, 0x01 });
    }

    [Fact]
    public void EscPosBuilder_ImageExtension_AppendsRasterBitCommand()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        var rgb = new byte[8 * 8 * 3]; // 8x8 image
        builder.Image(8, 8, rgb, bytesPerPixel: 3, EscPosDitherAlgorithm.Threshold);

        var doc = builder.Build();
        var bytes = doc.GetBytes();

        bytes.Should().StartWith(new byte[] { 0x1D, 0x76, 0x30 });
    }

    [Fact]
    public void EscPosBuilder_RasterImage_AppendsPrepackedRasterDirectly()
    {
        using var builder = new EscPosBuilder(safeMode: false);
        byte[] prepacked = [0xAA, 0x55];
        builder.RasterImage(16, 1, prepacked, scale: 0);

        var doc = builder.Build();
        var bytes = doc.GetBytes();

        bytes.Should().BeEquivalentTo(new byte[]
        {
            0x1D, 0x76, 0x30, 0x00,
            0x02, 0x00,
            0x01, 0x00,
            0xAA, 0x55
        });
    }

    [Fact]
    public void EscPosBuilder_ImageExtensions_NullGuardsThrowArgumentNullException()
    {
        var builder = new EscPosBuilder();
        var bmp = CreateTestBmp(2, 2, 24);

        var actNullBuilderBmp = () => EscPosImageBuilderExtensions.Image(null!, bmp);
        var actNullBmp = () => builder.Image((byte[])null!);
        var actNullBuilderRgb = () => EscPosImageBuilderExtensions.Image(null!, 2, 2, [0, 0, 0]);
        var actNullBuilderRaster = () => EscPosImageBuilderExtensions.RasterImage(null!, 2, 2, [0]);

        actNullBuilderBmp.Should().Throw<ArgumentNullException>();
        actNullBmp.Should().Throw<ArgumentNullException>();
        actNullBuilderRgb.Should().Throw<ArgumentNullException>();
        actNullBuilderRaster.Should().Throw<ArgumentNullException>();
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
        var bmpExceed = CreateTestBmp(105, 10, 24);
        var actExceed = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(
            bmpExceed, maxDimension: 100);
        actExceed.Should().Throw<System.IO.InvalidDataException>()
            .WithMessage("*Unsupported BMP width: 105. Must be between 1 and 100.*");

        var bmpValid = CreateTestBmp(100, 10, 24);
        var actValid = () => MonochromeBitmapConverter.ConvertBmpToPacked1Bit(
            bmpValid, maxDimension: 100);
        actValid.Should().NotThrow();
    }

    private static byte[] CreateTestBmp(int width, int height, short bitsPerPixel)
    {
        var absH = Math.Abs(height);
        var w = Math.Max(width, 1);
        var bytesPerPixel = bitsPerPixel / 8;
        var rowStride = (w * bytesPerPixel + 3) & ~3;
        var pixelDataSize = rowStride * absH;
        var totalFileSize = 54 + pixelDataSize;

        var bmp = new byte[totalFileSize];
        // Magic 'BM'
        bmp[0] = 0x42;
        bmp[1] = 0x4D;
        BitConverter.GetBytes(totalFileSize).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(width).CopyTo(bmp, 18);
        BitConverter.GetBytes(height).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes(bitsPerPixel).CopyTo(bmp, 28);

        // Fill pixel data
        for (var i = 54; i < totalFileSize; i++)
        {
            bmp[i] = (byte)(i % 256);
        }

        return bmp;
    }
}


