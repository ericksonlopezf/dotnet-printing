// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Printing.Zpl.Imaging;

/// <summary>
/// Provides extension methods for appending graphical bitmap images and graphic fields to <see cref="ZplBuilder"/>.
/// </summary>
public static class ZplImageBuilderExtensions
{
    /// <summary>
    /// Converts and appends an uncompressed BMP image as a ZPL II <c>^GF</c> Graphic Field.
    /// </summary>
    /// <param name="builder">The ZPL builder instance to append to</param>
    /// <param name="x">The horizontal origin coordinate in dots</param>
    /// <param name="y">The vertical origin coordinate in dots</param>
    /// <param name="bmpBytes">The raw bytes of the BMP file</param>
    /// <param name="algorithm">The dithering algorithm to apply. Default is <see cref="ZplDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="maxAllowedDimension">The maximum allowable width or height in pixels</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="bmpBytes"/> is <see langword="null"/></exception>
    public static ZplBuilder Image(
        this ZplBuilder builder,
        int x,
        int y,
        byte[] bmpBytes,
        ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg,
        int maxAllowedDimension = MonochromeBitmapConverter.MaxDimension)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(bmpBytes);

        var (width, height, packed) = MonochromeBitmapConverter.ConvertBmpToPacked1Bit(bmpBytes, algorithm, maxDimension: maxAllowedDimension);
        var command = ZplGraphicFieldConverter.BuildGraphicFieldCommand(x, y, width, height, packed);
        return builder.Raw(command);
    }

    /// <summary>
    /// Converts and appends raw pixel data as a ZPL II <c>^GF</c> Graphic Field.
    /// </summary>
    /// <param name="builder">The ZPL builder instance to append to</param>
    /// <param name="x">The horizontal origin coordinate in dots</param>
    /// <param name="y">The vertical origin coordinate in dots</param>
    /// <param name="width">The image width in pixels</param>
    /// <param name="height">The image height in pixels</param>
    /// <param name="pixelData">The raw pixel bytes in RGB, RGBA, or grayscale format</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel: 1 for grayscale, 3 for RGB, or 4 for RGBA</param>
    /// <param name="algorithm">The dithering algorithm to apply. Default is <see cref="ZplDitherAlgorithm.FloydSteinberg"/>.</param>
    /// <param name="maxAllowedDimension">The maximum allowable width or height in pixels</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static ZplBuilder Image(
        this ZplBuilder builder,
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> pixelData,
        int bytesPerPixel = 3,
        ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg,
        int maxAllowedDimension = MonochromeBitmapConverter.MaxDimension)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var packed = MonochromeBitmapConverter.ConvertRgbToPacked1Bit(width, height, pixelData, bytesPerPixel, algorithm, maxDimension: maxAllowedDimension);
        var command = ZplGraphicFieldConverter.BuildGraphicFieldCommand(x, y, width, height, packed);
        return builder.Raw(command);
    }

    /// <summary>
    /// Appends pre-packed 1-bit monochrome raster bytes as a ZPL II <c>^GF</c> Graphic Field.
    /// </summary>
    /// <param name="builder">The ZPL builder instance to append to</param>
    /// <param name="x">The horizontal origin coordinate in dots</param>
    /// <param name="y">The vertical origin coordinate in dots</param>
    /// <param name="width">The image width in dots</param>
    /// <param name="height">The image height in dots</param>
    /// <param name="packedRaster">The packed 1-bit raster data where 1 represents a black dot and 0 represents white</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static ZplBuilder GraphicField(
        this ZplBuilder builder,
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> packedRaster)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var command = ZplGraphicFieldConverter.BuildGraphicFieldCommand(x, y, width, height, packedRaster);
        return builder.Raw(command);
    }
}
