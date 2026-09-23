// Copyright © Erickson Lopez. MIT License.
using System;
using System.Globalization;
using System.Text;

namespace EricksonLopez.Printing.Zpl.Imaging;

/// <summary>
/// Converts monochrome pixel buffers and bitmaps into Zebra ZPL II <c>^GF</c> (Graphic Field) hex command streams.
/// </summary>
public static class ZplGraphicFieldConverter
{
    /// <summary>
    /// Builds a ZPL II <c>^GF</c> command sequence from 1-bit packed monochrome raster data.
    /// </summary>
    /// <param name="x">The horizontal origin coordinate in dots</param>
    /// <param name="y">The vertical origin coordinate in dots</param>
    /// <param name="width">The image width in dots. Must be greater than zero.</param>
    /// <param name="height">The image height in dots. Must be greater than zero.</param>
    /// <param name="packedRaster">The 1-bit packed raster data where 1 represents a black dot and 0 represents white</param>
    /// <returns>A formatted ZPL string representing the graphic field command sequence.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than or equal to zero</exception>
    /// <exception cref="ArgumentException"><paramref name="packedRaster"/> length is smaller than the required byte count for the specified dimensions</exception>
    public static string BuildGraphicFieldCommand(
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> packedRaster)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        var rowBytes = (width + 7) / 8;
        var totalBytes = rowBytes * height;

        if (packedRaster.Length < totalBytes)
        {
            throw new ArgumentException(
                $"Packed raster data length ({packedRaster.Length}) is smaller than required ({totalBytes}) for {width}x{height} image.",
                nameof(packedRaster));
        }

        var hex = Convert.ToHexString(packedRaster[..totalBytes]);

        var sb = new StringBuilder(hex.Length + 64);
        sb.Append(CultureInfo.InvariantCulture, $"^FO{x},{y}");
        sb.Append(CultureInfo.InvariantCulture, $"^GFA,{totalBytes},{totalBytes},{rowBytes},{hex}");
        sb.AppendLine("^FS");

        return sb.ToString();
    }
}
