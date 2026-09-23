// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.EscPos.Imaging;
using EricksonLopez.Printing.Zpl;
using EricksonLopez.Printing.Zpl.Imaging;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates advanced bitmap imaging, Floyd-Steinberg and threshold dithering, and custom raster conversion for ESC/POS and ZPL.
/// </summary>
public static class Level04AdvancedIntegration
{
    /// <summary>
    /// Executes the advanced imaging and graphics showcase demonstration asynchronously.
    /// </summary>
    public static Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 04: ADVANCED INTEGRATION — MONOCHROME IMAGING & RASTERIZATION");
        Console.WriteLine("================================================================================\n");

        // 1. Synthesizing an in-memory 24-bit BMP image (64x64 checkerboard / gradient logo)
        Console.WriteLine("[1] Synthesizing a 64x64 24-bit BMP image in memory:");
        var bmpBytes = GenerateSampleBmp(width: 64, height: 64);
        Console.WriteLine($"    ✔ Synthesized uncompressed 24-bit BMP buffer ({bmpBytes.Length} bytes).");

        // 2. ESC/POS Bitmap Printing with Floyd-Steinberg and Threshold dithering
        Console.WriteLine("\n[2] Embedding BMP into EscPosBuilder with EscPosDitherAlgorithm.FloydSteinberg:");
        Console.WriteLine("    (Notice: EscPosBuilder defaults to safeMode: true. For raw raster commands, safeMode: false is required)");
        using (var escposWithImage = new EscPosBuilder(safeMode: false))
        {
            escposWithImage.Initialize()
                           .Align(EscPosAlignment.Center)
                           .Line("STORE LOGO (FLOYD-STEINBERG DITHERED):")
                           .Image(bmpBytes, EscPosDitherAlgorithm.FloydSteinberg, scale: 0)
                           .Feed(1)
                           .Line("STORE LOGO (THRESHOLD DITHERED):")
                           .Image(bmpBytes, EscPosDitherAlgorithm.Threshold, scale: 1) // Double width
                           .Feed(2)
                           .Cut();

            var doc = escposWithImage.Build("LogoReceipt");
            Console.WriteLine($"    ✔ ESC/POS receipt with embedded dithered raster images compiled ({doc.GetBytes().Length} bytes).");
        }

        // Demonstrating that safeMode: true blocks raw command injection
        using (var safeBuilder = new EscPosBuilder(safeMode: true))
        {
            try
            {
                safeBuilder.Raw(bmpBytes.AsSpan(0, 10));
            }
            catch (NotSupportedException)
            {
                Console.WriteLine("    ✔ Security verified: safeMode=true successfully blocked raw binary injection.");
            }
        }

        // 3. Direct Raw Pixel Buffer Conversion to ESC/POS Raster Image
        Console.WriteLine("\n[3] Converting raw RGB buffer using MonochromeBitmapConverter directly:");
        const int rgbWidth = 32;
        const int rgbHeight = 16;
        var rawRgb = new byte[rgbWidth * rgbHeight * 3];
        // Fill half white, half dark gray
        Array.Fill(rawRgb, (byte)240, 0, rawRgb.Length / 2);
        Array.Fill(rawRgb, (byte)40, rawRgb.Length / 2, rawRgb.Length / 2);

        var packedEscPosRaster = EricksonLopez.Printing.EscPos.Imaging.MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            rgbWidth, rgbHeight, rawRgb, bytesPerPixel: 3, EscPosDitherAlgorithm.Threshold);
        var rasterCommand = EricksonLopez.Printing.EscPos.Imaging.MonochromeBitmapConverter.BuildEscPosRasterCommand(
            rgbWidth, rgbHeight, packedEscPosRaster, scale: 0);

        Console.WriteLine($"    ✔ Raw RGB converted to packed 1-bit raster: {packedEscPosRaster.Length} bytes.");
        Console.WriteLine($"    ✔ ESC/POS GS v 0 raster command built: {rasterCommand.Length} bytes.");

        // 4. Zebra ZPL II Image Rendering with ^GF (Graphic Field)
        Console.WriteLine("\n[4] Embedding graphic logo into ZplBuilder via ^GF command:");
        var zplBuilder = new ZplBuilder();
        zplBuilder.Text(50, 30, "LABEL WITH EMBEDDED GRAPHIC FIELD (^GF)", ZplFont.Font0, height: 25, width: 25)
                  .Image(x: 50, y: 70, bmpBytes, ZplDitherAlgorithm.FloydSteinberg)
                  .Box(40, 60, 200, 200, borderThickness: 2);

        // 5. Direct ZPL Graphic Field Conversion via ZplGraphicFieldConverter
        Console.WriteLine("\n[5] Generating isolated ZPL ^GF hex commands via ZplGraphicFieldConverter:");
        var packedZplRaster = EricksonLopez.Printing.Zpl.Imaging.MonochromeBitmapConverter.ConvertRgbToPacked1Bit(
            rgbWidth, rgbHeight, rawRgb, bytesPerPixel: 3, ZplDitherAlgorithm.FloydSteinberg);
        var gfCommandString = ZplGraphicFieldConverter.BuildGraphicFieldCommand(
            x: 300, y: 70, width: rgbWidth, height: rgbHeight, packedZplRaster);

        Console.WriteLine($"    ✔ Generated ZPL ^GF Command snippet:");
        Console.WriteLine($"      {gfCommandString.Trim()}");

        // Appending raw raster via GraphicField extension
        zplBuilder.GraphicField(x: 300, y: 150, width: rgbWidth, height: rgbHeight, packedZplRaster);

        var zplDoc = zplBuilder.Build("ZplGraphicLabel");
        Console.WriteLine($"    ✔ ZPL Document with graphics compiled ({zplDoc.GetBytes().Length} bytes).");

        Console.WriteLine("\n✔ Level 04 completed successfully.\n");
        return Task.CompletedTask;
    }

    private static byte[] GenerateSampleBmp(int width, int height)
    {
        var rowBytes = ((width * 3 + 3) / 4) * 4; // 4-byte aligned rows
        var pixelDataSize = rowBytes * height;
        var fileSize = 54 + pixelDataSize;

        var buffer = new byte[fileSize];
        var span = buffer.AsSpan();

        // 1. BMP Header (14 bytes)
        span[0] = 0x42; // 'B'
        span[1] = 0x4D; // 'M'
        BitConverter.TryWriteBytes(span.Slice(2, 4), fileSize);
        BitConverter.TryWriteBytes(span.Slice(10, 4), 54); // Pixel offset

        // 2. DIB Header (BITMAPINFOHEADER - 40 bytes)
        BitConverter.TryWriteBytes(span.Slice(14, 4), 40); // Header size
        BitConverter.TryWriteBytes(span.Slice(18, 4), width);
        BitConverter.TryWriteBytes(span.Slice(22, 4), height);
        BitConverter.TryWriteBytes(span.Slice(26, 2), (short)1); // Color planes
        BitConverter.TryWriteBytes(span.Slice(28, 2), (short)24); // Bits per pixel (24-bit RGB)
        BitConverter.TryWriteBytes(span.Slice(34, 4), pixelDataSize);

        // 3. Fill image pixel data with pattern
        var pixelOffset = 54;
        for (int y = 0; y < height; y++)
        {
            var rowStart = pixelOffset + (y * rowBytes);
            for (int x = 0; x < width; x++)
            {
                var idx = rowStart + (x * 3);
                // Checkerboard pattern with gradient
                byte color = (byte)(((x / 8 + y / 8) % 2 == 0) ? (x * 4) : 255);
                span[idx] = color;     // Blue
                span[idx + 1] = color; // Green
                span[idx + 2] = color; // Red
            }
        }

        return buffer;
    }
}
