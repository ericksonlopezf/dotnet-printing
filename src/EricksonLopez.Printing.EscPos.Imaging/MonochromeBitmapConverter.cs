// Copyright © Erickson Lopez. MIT License.
using System;
using System.Buffers;
using System.IO;

namespace EricksonLopez.Printing.EscPos.Imaging;

/// <summary>
/// Provides conversion and dithering algorithms to transform pixel buffers and BMP images into 1-bit monochrome ESC/POS raster bit data.
/// </summary>
public static class MonochromeBitmapConverter
{
    /// <summary>
    /// Defines the default maximum allowable image dimension in pixels to prevent memory exhaustion.
    /// </summary>
    public const int MaxDimension = 8192;

    /// <summary>
    /// Converts raw RGB, RGBA, or grayscale pixel data into packed 1-bit monochrome raster bytes for thermal printers.
    /// </summary>
    /// <remarks>
    /// In ESC/POS raster format, bit 1 represents a printed black dot and bit 0 represents white blank space.
    /// </remarks>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="pixelData">The raw pixel bytes in RGB, RGBA, or grayscale format.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel (1, 3, or 4). Defaults to 3.</param>
    /// <param name="algorithm">The dithering algorithm to apply. Defaults to <see cref="EscPosDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="threshold">The luminance threshold value between 0 and 255. Defaults to 128.</param>
    /// <param name="maxDimension">The maximum allowable pixel width or height. Defaults to <see cref="MaxDimension"/>.</param>
    /// <returns>A byte array containing packed 1-bit monochrome raster data.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is non-positive or exceeds <paramref name="maxDimension"/>, or <paramref name="bytesPerPixel"/> is not 1, 3, or 4</exception>
    /// <exception cref="ArgumentException"><paramref name="pixelData"/> length is smaller than the required buffer size</exception>
    public static byte[] ConvertRgbToPacked1Bit(
        int width,
        int height,
        ReadOnlySpan<byte> pixelData,
        int bytesPerPixel = 3,
        EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg,
        byte threshold = 128,
        int maxDimension = MaxDimension)
    {
        if (width <= 0 || width > maxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive and not exceed " + maxDimension + " pixels.");
        }
        if (height <= 0 || height > maxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Width and height must be positive and not exceed " + maxDimension + " pixels.");
        }
        if (bytesPerPixel is not (1 or 3 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerPixel), "Supported bytesPerPixel values are 1, 3, or 4.");
        }

        var expectedMinLength = checked((long)width * height * bytesPerPixel);
        if (pixelData.Length < expectedMinLength)
        {
            throw new ArgumentException("Pixel data is smaller than required.", nameof(pixelData));
        }

        var widthInBytes = (width + 7) / 8;
        var output = new byte[widthInBytes * height];

        if (algorithm == EscPosDitherAlgorithm.Threshold)
        {
            for (var y = 0; y < height; y++)
            {
                var rowByteOffset = y * widthInBytes;
                for (var x = 0; x < width; x++)
                {
                    var pIndex = (y * width + x) * bytesPerPixel;
                    var lum = CalculateLuminance(pixelData, pIndex, bytesPerPixel);
                    if (lum < threshold)
                    {
                        output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                    }
                }
            }
            return output;
        }

        var currentRowErrors = ArrayPool<int>.Shared.Rent(width);
        var nextRowErrors = ArrayPool<int>.Shared.Rent(width);
        Array.Clear(currentRowErrors, 0, width);
        Array.Clear(nextRowErrors, 0, width);

        try
        {
            for (var y = 0; y < height; y++)
            {
                var rowByteOffset = y * widthInBytes;
                for (var x = 0; x < width; x++)
                {
                    var pIndex = (y * width + x) * bytesPerPixel;
                    var lum = CalculateLuminance(pixelData, pIndex, bytesPerPixel);
                    var oldPixel = lum + currentRowErrors[x];
                    var newPixel = oldPixel < threshold ? 0 : 255;
                    var error = oldPixel - newPixel;

                    if (newPixel == 0)
                    {
                        output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                    }

                    if (x + 1 < width)
                    {
                        currentRowErrors[x + 1] += (error * 7) / 16;
                    }
                    if (y + 1 < height)
                    {
                        if (x - 1 >= 0)
                        {
                            nextRowErrors[x - 1] += (error * 3) / 16;
                        }
                        nextRowErrors[x] += (error * 5) / 16;
                        if (x + 1 < width)
                        {
                            nextRowErrors[x + 1] += (error * 1) / 16;
                        }
                    }
                }
                (currentRowErrors, nextRowErrors) = (nextRowErrors, currentRowErrors);
                Array.Clear(nextRowErrors, 0, width);
            }
            return output;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(currentRowErrors);
            ArrayPool<int>.Shared.Return(nextRowErrors);
        }
    }

    /// <summary>
    /// Parses an uncompressed Windows Bitmap (BMP) file and converts it to packed 1-bit monochrome raster bytes.
    /// </summary>
    /// <remarks>
    /// Supports 24-bit RGB and 32-bit RGBA BMP image formats.
    /// </remarks>
    /// <param name="bmpBytes">The raw bytes of the BMP file.</param>
    /// <param name="algorithm">The dithering algorithm to apply. Defaults to <see cref="EscPosDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="threshold">The luminance threshold value between 0 and 255. Defaults to 128.</param>
    /// <param name="maxDimension">The maximum allowable pixel width or height. Defaults to <see cref="MaxDimension"/>.</param>
    /// <returns>A tuple containing the image width, height, and packed 1-bit monochrome raster byte array.</returns>
    /// <exception cref="ArgumentException"><paramref name="bmpBytes"/> is too small to contain a valid BMP header or lacks the 'BM' magic header</exception>
    /// <exception cref="InvalidDataException">The BMP header contains invalid dimensions or offsets</exception>
    /// <exception cref="NotSupportedException">The BMP bit depth is not 24-bit or 32-bit</exception>
    public static (int Width, int Height, byte[] PackedRaster) ConvertBmpToPacked1Bit(
        ReadOnlySpan<byte> bmpBytes,
        EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg,
        byte threshold = 128,
        int maxDimension = MaxDimension)
    {
        if (bmpBytes.Length < 54)
        {
            throw new ArgumentException("Invalid BMP data: header is too small.", nameof(bmpBytes));
        }
        if (bmpBytes[0] != 0x42 || bmpBytes[1] != 0x4D)
        {
            throw new ArgumentException("Invalid BMP data: missing 'BM' magic header.", nameof(bmpBytes));
        }

        var pixelOffset = BitConverter.ToInt32(bmpBytes.Slice(10, 4));
        var width = BitConverter.ToInt32(bmpBytes.Slice(18, 4));
        var rawHeight = BitConverter.ToInt32(bmpBytes.Slice(22, 4));
        var bitsPerPixel = BitConverter.ToInt16(bmpBytes.Slice(28, 2));

        if (rawHeight == int.MinValue)
        {
            throw new InvalidDataException("Invalid BMP raw height: int.MinValue is not supported.");
        }
        if (pixelOffset < 54 || pixelOffset >= bmpBytes.Length)
        {
            throw new InvalidDataException($"Invalid BMP pixel offset: {pixelOffset}.");
        }
        if (width <= 0 || width > maxDimension)
        {
            throw new InvalidDataException($"Unsupported BMP width: {width}. Must be between 1 and {maxDimension}.");
        }

        var height = Math.Abs(rawHeight);
        if (height == 0 || height > maxDimension)
        {
            throw new InvalidDataException($"Unsupported BMP height: {height}. Must be between 1 and {maxDimension}.");
        }
        var isTopDown = rawHeight < 0;

        if (bitsPerPixel is not (24 or 32))
        {
            throw new NotSupportedException($"Only 24-bit and 32-bit BMPs are supported. Found: {bitsPerPixel}-bit.");
        }

        var bytesPerPixel = bitsPerPixel / 8;
        var rowStride = (width * bytesPerPixel + 3) & ~3;
        var requiredDataSize = checked((long)pixelOffset + ((long)height * rowStride));
        if (bmpBytes.Length < requiredDataSize)
        {
            throw new InvalidDataException($"BMP payload length ({bmpBytes.Length}) is smaller than required image data ({requiredDataSize}).");
        }

        var widthInBytes = (width + 7) / 8;
        var output = new byte[widthInBytes * height];
        var rentedRow = ArrayPool<byte>.Shared.Rent(width * 3);
        var currentRowErrors = ArrayPool<int>.Shared.Rent(width);
        var nextRowErrors = ArrayPool<int>.Shared.Rent(width);
        Array.Clear(currentRowErrors, 0, width);
        Array.Clear(nextRowErrors, 0, width);

        try
        {
            var rowSpan = rentedRow.AsSpan(0, width * 3);

            for (var y = 0; y < height; y++)
            {
                var srcY = isTopDown ? y : (height - 1 - y);
                var srcRowStart = pixelOffset + (srcY * rowStride);

                // Unpack one row
                for (var x = 0; x < width; x++)
                {
                    var srcPixel = srcRowStart + (x * bytesPerPixel);
                    var destPixel = x * 3;
                    rowSpan[destPixel] = bmpBytes[srcPixel + 2]; // R
                    rowSpan[destPixel + 1] = bmpBytes[srcPixel + 1]; // G
                    rowSpan[destPixel + 2] = bmpBytes[srcPixel]; // B
                }

                var rowByteOffset = y * widthInBytes;
                for (var x = 0; x < width; x++)
                {
                    var pIndex = x * 3;
                    var r = rowSpan[pIndex];
                    var g = rowSpan[pIndex + 1];
                    var b = rowSpan[pIndex + 2];
                    var lum = (r * 299 + g * 587 + b * 114) / 1000;

                    if (algorithm == EscPosDitherAlgorithm.Threshold)
                    {
                        if (lum < threshold)
                        {
                            output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                        }
                    }
                    else
                    {
                        var oldPixel = lum + currentRowErrors[x];
                        var newPixel = oldPixel < threshold ? 0 : 255;
                        var error = oldPixel - newPixel;

                        if (newPixel == 0)
                        {
                            output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                        }

                        if (x + 1 < width)
                        {
                            currentRowErrors[x + 1] += (error * 7) / 16;
                        }
                        if (y + 1 < height)
                        {
                            if (x - 1 >= 0)
                            {
                                nextRowErrors[x - 1] += (error * 3) / 16;
                            }
                            nextRowErrors[x] += (error * 5) / 16;
                            if (x + 1 < width)
                            {
                                nextRowErrors[x + 1] += (error * 1) / 16;
                            }
                        }
                    }
                }

                if (algorithm != EscPosDitherAlgorithm.Threshold)
                {
                    (currentRowErrors, nextRowErrors) = (nextRowErrors, currentRowErrors);
                    Array.Clear(nextRowErrors, 0, width);
                }
            }

            return (width, height, output);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedRow);
            ArrayPool<int>.Shared.Return(currentRowErrors);
            ArrayPool<int>.Shared.Return(nextRowErrors);
        }
    }

    /// <summary>
    /// Creates an ESC/POS raster bit image command from packed monochrome raster data.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="packedRaster">The packed 1-bit raster data.</param>
    /// <param name="scale">The scaling factor mode (0 to 3). Defaults to 0.</param>
    /// <returns>A byte array containing the complete ESC/POS raster graphics command.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> exceeds 3, <paramref name="width"/> or <paramref name="height"/> is non-positive, or dimensions exceed 65535</exception>
    /// <exception cref="ArgumentException"><paramref name="packedRaster"/> is smaller than the required packed byte size</exception>
    public static byte[] BuildEscPosRasterCommand(
        int width,
        int height,
        ReadOnlySpan<byte> packedRaster,
        byte scale = 0)
    {
        if (scale > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale mode must be between 0 and 3.");
        }
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        var widthInBytes = (width + 7) / 8;
        if (widthInBytes > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width exceeds max 16-bit raster limit.");
        }
        if (height > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height exceeds max 16-bit raster limit.");
        }

        if (packedRaster.Length < widthInBytes * height)
        {
            throw new ArgumentException("Packed raster data too small.", nameof(packedRaster));
        }

        var xL = (byte)(widthInBytes & 0xFF);
        var xH = (byte)((widthInBytes >> 8) & 0xFF);
        var yL = (byte)(height & 0xFF);
        var yH = (byte)((height >> 8) & 0xFF);

        var header = new byte[] { 0x1D, 0x76, 0x30, scale, xL, xH, yL, yH };
        var result = new byte[header.Length + packedRaster.Length];
        header.CopyTo(result, 0);
        packedRaster.CopyTo(result.AsSpan(header.Length));
        return result;
    }

    private static int CalculateLuminance(ReadOnlySpan<byte> data, int offset, int bytesPerPixel)
    {
        if (bytesPerPixel == 1)
        {
            return data[offset];
        }
        var r = data[offset];
        var g = data[offset + 1];
        var b = data[offset + 2];
        return (r * 299 + g * 587 + b * 114) / 1000;
    }
}



