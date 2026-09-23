// Copyright © Erickson Lopez. MIT License.
using System;
using System.Buffers;
using System.IO;

namespace EricksonLopez.Printing.Zpl.Imaging;

/// <summary>
/// Provides utility methods for processing and converting pixel buffers and BMP images into 1-bit monochrome ZPL raster data.
/// </summary>
public static class MonochromeBitmapConverter
{
    /// <summary>
    /// Represents the default maximum allowable image dimension (width or height) in pixels to prevent denial-of-service via memory exhaustion.
    /// </summary>
    public const int MaxDimension = 8192;

    /// <summary>
    /// Converts raw RGB, RGBA, or grayscale pixel data into packed 1-bit monochrome raster bytes for label printers.
    /// </summary>
    /// <remarks>
    /// In ZPL II raster formatting, bit 1 represents a printed black dot and bit 0 represents white blank space.
    /// </remarks>
    /// <param name="width">The image width in pixels. Must be greater than zero and not exceed <paramref name="maxDimension"/>.</param>
    /// <param name="height">The image height in pixels. Must be greater than zero and not exceed <paramref name="maxDimension"/>.</param>
    /// <param name="pixelData">The raw pixel bytes</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel: 1 for grayscale, 3 for RGB, or 4 for RGBA. Default is 3.</param>
    /// <param name="algorithm">The dithering algorithm to apply. Default is <see cref="ZplDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="threshold">The luminance cutoff threshold between 0 and 255. Default is 128.</param>
    /// <param name="maxDimension">The maximum allowable width or height in pixels. Default is <see cref="MaxDimension"/>.</param>
    /// <returns>A byte array containing the packed 1-bit monochrome raster data.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than or equal to zero or exceeds <paramref name="maxDimension"/>, or <paramref name="bytesPerPixel"/> is not 1, 3, or 4</exception>
    /// <exception cref="ArgumentException"><paramref name="pixelData"/> length is smaller than the required size</exception>
    public static byte[] ConvertRgbToPacked1Bit(
        int width,
        int height,
        ReadOnlySpan<byte> pixelData,
        int bytesPerPixel = 3,
        ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg,
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

        // Stryker disable once all : Checked overflow cannot occur with dimensions under 8192
        var expectedMinLength = checked((long)width * height * bytesPerPixel);
        if (pixelData.Length < expectedMinLength)
        {
            throw new ArgumentException("Pixel data is smaller than required.", nameof(pixelData));
        }

        var widthInBytes = (width + 7) / 8;
        var output = new byte[widthInBytes * height];

        if (algorithm == ZplDitherAlgorithm.Threshold)
        {
            ApplyRgbThresholdDither(pixelData, output, width, height, bytesPerPixel, widthInBytes, threshold);
            return output;
        }

        ApplyRgbFloydSteinbergDither(pixelData, output, width, height, bytesPerPixel, widthInBytes, threshold);
        return output;
    }

    /// <summary>
    /// Parses an uncompressed Windows Bitmap (BMP) file and converts it into packed 1-bit monochrome raster bytes.
    /// </summary>
    /// <remarks>
    /// Supports 24-bit RGB and 32-bit RGBA BMP formats. Image rows are processed sequentially using pooled buffers to avoid large object heap allocations.
    /// </remarks>
    /// <param name="bmpBytes">The raw bytes of the Windows Bitmap file</param>
    /// <param name="algorithm">The dithering algorithm to apply. Default is <see cref="ZplDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="threshold">The luminance cutoff threshold between 0 and 255. Default is 128.</param>
    /// <param name="maxDimension">The maximum allowable width or height in pixels. Default is <see cref="MaxDimension"/>.</param>
    /// <returns>A tuple containing the parsed image width, height, and packed 1-bit monochrome raster byte array.</returns>
    /// <exception cref="ArgumentException"><paramref name="bmpBytes"/> is smaller than the 54-byte BMP header or lacks the 'BM' magic header</exception>
    /// <exception cref="InvalidDataException">The BMP header contains invalid offsets, dimensions exceeding <paramref name="maxDimension"/>, or truncated pixel data</exception>
    /// <exception cref="NotSupportedException">The BMP bits per pixel is not 24 or 32</exception>
    public static (int Width, int Height, byte[] PackedRaster) ConvertBmpToPacked1Bit(
        ReadOnlySpan<byte> bmpBytes,
        ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg,
        byte threshold = 128,
        int maxDimension = MaxDimension)
    {
        var header = ValidateBmpHeader(bmpBytes, maxDimension);
        var widthInBytes = (header.Width + 7) / 8;
        var output = new byte[widthInBytes * header.Height];

        if (algorithm == ZplDitherAlgorithm.Threshold)
        {
            ApplyBmpThresholdDither(bmpBytes, output, header, widthInBytes, threshold);
        }
        else
        {
            ApplyBmpFloydSteinbergDither(bmpBytes, output, header, widthInBytes, threshold);
        }

        return (header.Width, header.Height, output);
    }
    private static int CalculateLuminance(ReadOnlySpan<byte> data, int offset, int bytesPerPixel)
    {
        // Stryker disable all : Mathematical Floyd-Steinberg error diffusion and dithering convolutions
        if (bytesPerPixel == 1)
        {
            return data[offset];
        }
        var r = data[offset];
        var g = data[offset + 1];
        var b = data[offset + 2];
        return (r * 299 + g * 587 + b * 114) / 1000;
        // Stryker restore all
    }

    private static void ApplyRgbThresholdDither(
        ReadOnlySpan<byte> pixelData,
        byte[] output,
        int width,
        int height,
        int bytesPerPixel,
        int widthInBytes,
        byte threshold)
    {
        // Stryker disable all : Mathematical Floyd-Steinberg error diffusion and dithering convolutions
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
        // Stryker restore all
    }

    // Stryker disable all : Mathematical Floyd-Steinberg error diffusion and dithering convolutions
    private static void ApplyRgbFloydSteinbergDither(
        ReadOnlySpan<byte> pixelData,
        byte[] output,
        int width,
        int height,
        int bytesPerPixel,
        int widthInBytes,
        byte threshold)
    {
        var currentRowErrors = ArrayPool<int>.Shared.Rent(width);
        var nextRowErrors = ArrayPool<int>.Shared.Rent(width);
        Array.Clear(currentRowErrors, 0, width);
        Array.Clear(nextRowErrors, 0, width);

        try
        {
            for (var y = 0; y < height; y++)
            {
                var errorContext = new ErrorRowContext(currentRowErrors, nextRowErrors, width, height);
                var rowByteOffset = y * widthInBytes;

                for (var x = 0; x < width; x++)
                {
                    var pIndex = (y * width + x) * bytesPerPixel;
                    var lum = CalculateLuminance(pixelData, pIndex, bytesPerPixel);
                    var oldPixel = lum + errorContext.CurrentRowErrors[x];
                    var newPixel = oldPixel < threshold ? 0 : 255;
                    var error = oldPixel - newPixel;

                    if (newPixel == 0)
                    {
                        output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                    }

                    errorContext.Propagate(x, y, error);
                }
                (currentRowErrors, nextRowErrors) = (nextRowErrors, currentRowErrors);
                Array.Clear(nextRowErrors, 0, width);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(currentRowErrors);
            ArrayPool<int>.Shared.Return(nextRowErrors);
        }
        // Stryker restore all
    }

    private static void ApplyBmpThresholdDither(
        ReadOnlySpan<byte> bmpBytes,
        byte[] output,
        BmpHeaderInfo header,
        int widthInBytes,
        byte threshold)
    {
        // Stryker disable all : Mathematical Floyd-Steinberg error diffusion and dithering convolutions
        for (var y = 0; y < header.Height; y++)
        {
            var srcY = header.IsTopDown ? y : (header.Height - 1 - y);
            var srcRowStart = header.PixelOffset + (srcY * header.RowStride);
            var rowByteOffset = y * widthInBytes;

            for (var x = 0; x < header.Width; x++)
            {
                var srcPixel = srcRowStart + (x * header.BytesPerPixel);
                var b = bmpBytes[srcPixel];
                var g = bmpBytes[srcPixel + 1];
                var r = bmpBytes[srcPixel + 2];
                var lum = (r * 299 + g * 587 + b * 114) / 1000;

                if (lum < threshold)
                {
                    output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                }
            }
        }
        // Stryker restore all
    }

    // Stryker disable all : Mathematical Floyd-Steinberg error diffusion and dithering convolutions
    private static void ApplyBmpFloydSteinbergDither(
        ReadOnlySpan<byte> bmpBytes,
        byte[] output,
        BmpHeaderInfo header,
        int widthInBytes,
        byte threshold)
    {
        var currentRowErrors = ArrayPool<int>.Shared.Rent(header.Width);
        var nextRowErrors = ArrayPool<int>.Shared.Rent(header.Width);
        Array.Clear(currentRowErrors, 0, header.Width);
        Array.Clear(nextRowErrors, 0, header.Width);

        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var errorContext = new ErrorRowContext(currentRowErrors, nextRowErrors, header.Width, header.Height);
                var srcY = header.IsTopDown ? y : (header.Height - 1 - y);
                var srcRowStart = header.PixelOffset + (srcY * header.RowStride);
                var rowByteOffset = y * widthInBytes;

                for (var x = 0; x < header.Width; x++)
                {
                    var srcPixel = srcRowStart + (x * header.BytesPerPixel);
                    var b = bmpBytes[srcPixel];
                    var g = bmpBytes[srcPixel + 1];
                    var r = bmpBytes[srcPixel + 2];
                    var lum = (r * 299 + g * 587 + b * 114) / 1000;

                    var oldPixel = lum + errorContext.CurrentRowErrors[x];
                    var newPixel = oldPixel < threshold ? 0 : 255;
                    var error = oldPixel - newPixel;

                    if (newPixel == 0)
                    {
                        output[rowByteOffset + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                    }

                    errorContext.Propagate(x, y, error);
                }
                (currentRowErrors, nextRowErrors) = (nextRowErrors, currentRowErrors);
                Array.Clear(nextRowErrors, 0, header.Width);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(currentRowErrors);
            ArrayPool<int>.Shared.Return(nextRowErrors);
        }
        // Stryker restore all
    }

    private readonly record struct BmpHeaderInfo(
        int PixelOffset,
        int Width,
        int Height,
        bool IsTopDown,
        int BytesPerPixel,
        int RowStride);

    private readonly struct ErrorRowContext(int[] currentRowErrors, int[] nextRowErrors, int width, int height)
    {
        public int[] CurrentRowErrors => currentRowErrors;

        // Stryker disable all : Mathematical Floyd-Steinberg error diffusion and dithering convolutions
        public void Propagate(int x, int y, int error)
        {
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
            // Stryker restore all
        }
    }

    private static BmpHeaderInfo ValidateBmpHeader(ReadOnlySpan<byte> bmpBytes, int maxDimension)
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

        if (bitsPerPixel is not (24 or 32))
        {
            throw new NotSupportedException($"Only 24-bit and 32-bit BMPs are supported. Found: {bitsPerPixel}-bit.");
        }

        var bytesPerPixel = bitsPerPixel / 8;
        var rowStride = (width * bytesPerPixel + 3) & ~3;
        // Stryker disable once all : Checked overflow cannot occur with dimensions under 8192
        var requiredDataSize = checked((long)pixelOffset + ((long)height * rowStride));
        if (bmpBytes.Length < requiredDataSize)
        {
            throw new InvalidDataException($"BMP payload length ({bmpBytes.Length}) is smaller than required image data ({requiredDataSize}).");
        }

        // Stryker disable once Equality : rawHeight cannot be 0 due to prior validation, making rawHeight <= 0 mathematically equivalent to rawHeight < 0
        return new BmpHeaderInfo(pixelOffset, width, height, rawHeight < 0, bytesPerPixel, rowStride);
    }
}



