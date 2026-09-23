// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.Printing.EscPos.Imaging;

/// <summary>
/// Provides extension methods for appending graphic bitmap images to an <see cref="EscPosBuilder"/>.
/// </summary>
public static class EscPosImageBuilderExtensions
{
    /// <summary>
    /// Converts and appends an uncompressed BMP image (24-bit or 32-bit) to the ESC/POS receipt buffer.
    /// </summary>
    /// <param name="builder">The ESC/POS builder instance.</param>
    /// <param name="bmpBytes">The raw bytes of the BMP file.</param>
    /// <param name="algorithm">The dithering algorithm to apply. Defaults to <see cref="EscPosDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="scale">The scale factor (0=normal, 1=double width, 2=double height, 3=quadruple). Defaults to 0.</param>
    /// <param name="maxAllowedDimension">The maximum allowable pixel dimension. Defaults to <see cref="MonochromeBitmapConverter.MaxDimension"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="bmpBytes"/> is <see langword="null"/></exception>
    public static EscPosBuilder Image(
        this EscPosBuilder builder,
        byte[] bmpBytes,
        EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg,
        byte scale = 0,
        int maxAllowedDimension = MonochromeBitmapConverter.MaxDimension)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(bmpBytes);

        var (width, height, packed) = MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmpBytes, algorithm, maxDimension: maxAllowedDimension);
        var command = MonochromeBitmapConverter.BuildEscPosRasterCommand(width, height, packed, scale);
        return builder.Raw(command);
    }

    /// <summary>
    /// Converts and appends raw pixel data to the ESC/POS receipt buffer.
    /// </summary>
    /// <param name="builder">The ESC/POS builder instance.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="pixelData">The raw pixel bytes in RGB, RGBA, or grayscale format.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel (1, 3, or 4). Defaults to 3.</param>
    /// <param name="algorithm">The dithering algorithm to apply. Defaults to <see cref="EscPosDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="scale">The scale factor (0=normal, 1=double width, 2=double height, 3=quadruple). Defaults to 0.</param>
    /// <param name="maxAllowedDimension">The maximum allowable pixel dimension. Defaults to <see cref="MonochromeBitmapConverter.MaxDimension"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Fluent builder extension method provides complete image raster configuration with sensible defaults.")]
    [SuppressMessage("csharpsquid", "S107", Justification = "Fluent builder extension method provides complete image raster configuration with sensible defaults.")]
    public static EscPosBuilder Image(
        this EscPosBuilder builder,
        int width,
        int height,
        ReadOnlySpan<byte> pixelData,
        int bytesPerPixel = 3,
        EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg,
        byte scale = 0,
        int maxAllowedDimension = MonochromeBitmapConverter.MaxDimension)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(width, height, pixelData, bytesPerPixel, algorithm, maxDimension: maxAllowedDimension);
        var command = MonochromeBitmapConverter.BuildEscPosRasterCommand(width, height, packed, scale);
        return builder.Raw(command);
    }

    /// <summary>
    /// Appends pre-packed 1-bit monochrome raster data to the ESC/POS receipt buffer.
    /// </summary>
    /// <param name="builder">The ESC/POS builder instance.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="packedRaster">The packed 1-bit raster data where 1 represents black and 0 represents white.</param>
    /// <param name="scale">The scale factor (0=normal, 1=double width, 2=double height, 3=quadruple). Defaults to 0.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static EscPosBuilder RasterImage(
        this EscPosBuilder builder,
        int width,
        int height,
        ReadOnlySpan<byte> packedRaster,
        byte scale = 0)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var command = MonochromeBitmapConverter.BuildEscPosRasterCommand(width, height, packedRaster, scale);
        return builder.Raw(command);
    }
}
