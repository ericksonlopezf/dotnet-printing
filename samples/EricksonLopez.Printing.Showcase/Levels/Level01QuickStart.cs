// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.Zpl;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates the minimal, standalone usage of ESC/POS and ZPL builders and raw documents without dependency injection.
/// </summary>
public static class Level01QuickStart
{
    /// <summary>
    /// Executes the quick start showcase demonstration asynchronously.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 01: QUICK START — FIRST COMPOSITION & COMPILATION");
        Console.WriteLine("================================================================================\n");

        // 1. Composing an Epson ESC/POS receipt
        Console.WriteLine("[1] Composing a POS receipt using EscPosBuilder:");
        IPrintDocument receiptDoc;
        using (var builder = new EscPosBuilder())
        {
            builder.Initialize()
                   .Align(EscPosAlignment.Center)
                   .Bold(true)
                   .Line("ACME COFFEE SHOP")
                   .Bold(false)
                   .Line("123 Tech Parkway, Silicon Valley")
                   .Divider(32, '=')
                   .Align(EscPosAlignment.Left)
                   .TableRow("Espresso Doppio", "$3.50", 32)
                   .TableRow("Almond Croissant", "$4.25", 32)
                   .TableRow("Bottled Water", "$1.75", 32)
                   .Divider(32, '-')
                   .Bold(true)
                   .TableRow("TOTAL USD", "$9.50", 32)
                   .Bold(false)
                   .Feed(2)
                   .Cut(partial: false);

            receiptDoc = builder.Build("CoffeeReceipt-001", idempotencyKey: "ORD-COFFEE-001");
        }

        Console.WriteLine($"    ✔ Receipt built: Name='{receiptDoc.DocumentName}', IdempotencyKey='{receiptDoc.IdempotencyKey}'");
        Console.WriteLine($"    Total byte count: {receiptDoc.GetBytes().Length} bytes, IsEmpty={receiptDoc.IsEmpty}, IsIdempotent={receiptDoc.IsIdempotent}");

        // 2. Composing a Zebra ZPL II shipping label
        Console.WriteLine("\n[2] Composing an industrial shipping label using ZplBuilder:");
        var zplBuilder = new ZplBuilder();
        zplBuilder.Text(50, 40, "EXPRESS LOGISTICS HUB", ZplFont.Font0, height: 35, width: 35)
                  .Text(50, 90, "SHIP TO: ACME DC #42", ZplFont.FontD, height: 20, width: 20)
                  .Box(40, 30, 720, 340, borderThickness: 3)
                  .BarcodeCode128(60, 140, "TRACK-987654321", height: 75, showText: true)
                  .QrCode(520, 130, "https://logistics.example.com/track/987654321", magnification: 4);

        var labelDoc = zplBuilder.Build("PalletLabel-987", idempotencyKey: "PLT-987");
        Console.WriteLine($"    ✔ ZPL Label built: Name='{labelDoc.DocumentName}', IdempotencyKey='{labelDoc.IdempotencyKey}'");
        Console.WriteLine($"    Total byte count: {labelDoc.GetBytes().Length} bytes");

        // 3. Streaming documents to an output stream via WriteToAsync
        Console.WriteLine("\n[3] Streaming document bytes to an asynchronous destination stream without full byte[] allocation:");
        using (var destinationStream = new MemoryStream())
        {
            await receiptDoc.WriteToAsync(destinationStream);
            Console.WriteLine($"    ✔ Wrote {destinationStream.Length} bytes of receipt data to destination stream.");
        }

        // 4. Using RawPrintDocument directly
        Console.WriteLine("\n[4] Creating an explicit RawPrintDocument with pre-encoded binary commands:");
        byte[] rawBytes = [0x1B, 0x40, 0x1B, 0x64, 0x01]; // ESC @, ESC d 1
        var rawDoc = new RawPrintDocument(rawBytes, "PulseDrawer", "RAW-001", isIdempotent: true);
        Console.WriteLine($"    ✔ RawPrintDocument created: Name='{rawDoc.DocumentName}', Length={rawDoc.GetMemory().Length}, IsIdempotent={rawDoc.IsIdempotent}");

        Console.WriteLine("\n✔ Level 01 completed successfully.\n");
    }
}
