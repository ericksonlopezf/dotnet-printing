// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.Zpl;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates end-to-end, concrete business printing scenarios derived directly from the public API surface.
/// </summary>
public static class Level03RealWorldUseCases
{
    /// <summary>
    /// Executes the real-world use cases showcase demonstration asynchronously.
    /// </summary>
    public static Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 03: REAL-WORLD USE CASES — RETAIL, RESTAURANTS & LOGISTICS");
        Console.WriteLine("================================================================================\n");

        // Scenario 1: Restaurant Kitchen Order & Customer Dining Receipt
        Console.WriteLine("[Scenario 1] Full Restaurant Kitchen Order Ticket & Fiscal Customer Bill (ESC/POS):");
        using (var kitchenBuilder = new EscPosBuilder())
        {
            kitchenBuilder.Initialize()
                          .Align(EscPosAlignment.Center)
                          .DoubleSize(doubleWidth: true, doubleHeight: true)
                          .Line("KITCHEN ORDER #104")
                          .DoubleSize(doubleWidth: false, doubleHeight: false)
                          .Line("TABLE: 12 — SERVER: MARCO")
                          .Line($"TIME: {DateTime.Now:HH:mm:ss}")
                          .Divider(42, '=')
                          .Align(EscPosAlignment.Left)
                          .Bold(true)
                          .Line("1x RIBEYE STEAK 16oz — MEDIUM RARE")
                          .Bold(false)
                          .Line("   * Side: Truffle Fries")
                          .Line("   * Sauce: Peppercorn on side")
                          .Feed(1)
                          .Bold(true)
                          .Line("2x PACIFIC SALMON — STEAMED")
                          .Bold(false)
                          .Line("   * ALLERGY ALERT: NO SHELLFISH CONTAM")
                          .Divider(42, '=')
                          .Feed(3)
                          .Cut(partial: true);

            var kitchenDoc = kitchenBuilder.Build("KitchenOrder-104", "KITCHEN-104");
            Console.WriteLine($"    ✔ Kitchen Order Ticket generated: {kitchenDoc.GetBytes().Length} bytes");
        }

        using (var billBuilder = new EscPosBuilder())
        {
            billBuilder.Initialize()
                       .Align(EscPosAlignment.Center)
                       .Bold(true)
                       .FontSize(widthMultiplier: 2, heightMultiplier: 2)
                       .Line("BELLA VISTA BISTRO")
                       .FontSize(widthMultiplier: 1, heightMultiplier: 1)
                       .Bold(false)
                       .Line("Tax ID: 99-88776655-K")
                       .Line("450 Coastal Avenue, Suite 10")
                       .Divider(42, '-')
                       .Align(EscPosAlignment.Left)
                       .TableRow("1x Ribeye Steak 16oz", "$48.00", 42)
                       .TableRow("2x Pacific Salmon ($32 ea)", "$64.00", 42)
                       .TableRow("1x Cabernet Sauvignon Bot", "$55.00", 42)
                       .TableRow("2x San Pellegrino 750ml", "$12.00", 42)
                       .Divider(42, '-')
                       .TableRow("Subtotal", "$179.00", 42)
                       .TableRow("State Tax (8.25%)", "$14.77", 42)
                       .TableRow("Suggested Tip (18%)", "$32.22", 42)
                       .Divider(42, '=')
                       .Bold(true)
                       .Underline(EscPosUnderline.DoubleDot)
                       .TableRow("TOTAL DUE USD", "$225.99", 42)
                       .Underline(EscPosUnderline.None)
                       .Bold(false)
                       .Feed(1)
                       .Align(EscPosAlignment.Center)
                       .Line("Scan to verify electronic invoice:")
                       .QrCode("https://billing.example.gov/verify?id=BBV-2026-9982", moduleSize: 5, errorCorrection: EscPosQrErrorCorrection.M)
                       .Feed(1)
                       .Line("THANK YOU FOR DINING WITH US!")
                       .OpenCashDrawer() // Pop register drawer for cash transactions
                       .Feed(3)
                       .Cut(partial: false);

            var billDoc = billBuilder.Build("CustomerBill-104", "BILL-104");
            Console.WriteLine($"    ✔ Customer Dining Receipt generated: {billDoc.GetBytes().Length} bytes");
        }

        // Scenario 2: Supermarket Retail with 1D Barcode Types (EAN-13, Code128, Code39)
        Console.WriteLine("\n[Scenario 2] Retail Supermarket Receipt with Multiple Barcode Formats (ESC/POS):");
        using (var retailBuilder = new EscPosBuilder())
        {
            retailBuilder.Initialize()
                         .Align(EscPosAlignment.Center)
                         .Bold(true)
                         .Line("SUPERMARKET EXPRESS")
                         .Bold(false)
                         .Divider(42, '-')
                         .Align(EscPosAlignment.Left)
                         .TableRow("Organic Whole Milk 1gal", "$4.99", 42)
                         .TableRow("Artisan Sourdough Loaf", "$5.49", 42)
                         .Divider(42, '-')
                         .TableRow("Total Items (2)", "$10.48", 42)
                         .Feed(1)
                         .Align(EscPosAlignment.Center)
                         .Line("Scanned Item EAN-13:")
                         .BarcodeEan13("750103131130", height: 50, width: 2, showHri: true)
                         .Feed(1)
                         .Line("Loyalty Club Member Code (Code 39):")
                         .BarcodeCode39("MEMBER99", height: 45, width: 2, showHri: true)
                         .Feed(1)
                         .Line("Return & Refund Barcode (Code 128):")
                         .BarcodeCode128("RET-20260914-001", height: 60, showHri: true)
                         .Feed(2)
                         .Cut();

            var retailDoc = retailBuilder.Build("RetailTicket-001");
            Console.WriteLine($"    ✔ Retail Receipt generated with EAN-13, Code 39, and Code 128: {retailDoc.GetBytes().Length} bytes");
        }

        // Scenario 3: Industrial Warehouse Logistics Pallet Label (ZPL II)
        Console.WriteLine("\n[Scenario 3] Industrial Warehouse Pallet Shipping Label (Zebra ZPL II):");
        var zplLabel = new ZplBuilder();
        zplLabel.Box(20, 20, 760, 1160, borderThickness: 4) // Outer border
                .Box(20, 180, 760, 4, borderThickness: 4)    // Horizontal separator
                .Box(20, 600, 760, 4, borderThickness: 4)    // Horizontal separator
                .Box(500, 20, 4, 160, borderThickness: 4)    // Vertical separator
                                                             // Header zone
                .Text(40, 40, "FROM: GLOBAL MFG LOGISTICS", ZplFont.Font0, height: 25, width: 25)
                .Text(40, 75, "FACILITY: PLANT #04, DALLAS TX", ZplFont.Font0, height: 22, width: 22)
                .Text(40, 110, "CARRIER: FEDEX FREIGHT PRIORITY", ZplFont.Font0, height: 22, width: 22)
                .Text(520, 40, "POSTAL CODE", ZplFont.Font0, height: 20, width: 20)
                .Text(520, 70, "(420) 75001", ZplFont.Font0, height: 40, width: 35)
                // Shipping zone
                .Text(40, 210, "SHIP TO:", ZplFont.Font0, height: 25, width: 25)
                .Text(40, 250, "DISTRIBUTION CENTER NORTH", ZplFont.Font0, height: 35, width: 35)
                .Text(40, 300, "BUILDING 4B, BAY 18", ZplFont.Font0, height: 30, width: 30)
                .Text(40, 340, "CHICAGO, IL 60601", ZplFont.Font0, height: 30, width: 30)
                // Geometric shapes: Warning inspection badge and routing ellipse
                .Circle(660, 260, diameter: 80, borderThickness: 3)
                .Text(645, 290, "PASS", ZplFont.Font0, height: 25, width: 20)
                .Ellipse(40, 400, width: 220, height: 80, borderThickness: 3)
                .Text(65, 430, "STANDARD LTL", ZplFont.Font0, height: 25, width: 25)
                .DiagonalLine(500, 400, width: 100, height: 100, borderThickness: 3, rightLeaning: true)
                // Barcode zones
                .Text(40, 630, "SSCC-18 SERIAL SHIPPING CONTAINER CODE:", ZplFont.Font0, height: 22, width: 22)
                .BarcodeCode128(40, 670, "001084692000000001", height: 120, showText: true)
                .Text(40, 840, "PRODUCT GTIN-13:", ZplFont.Font0, height: 22, width: 22)
                .BarcodeEan13(40, 875, "750103131130", height: 75, showText: true)
                .Text(40, 990, "INTERNAL BIN ID:", ZplFont.Font0, height: 22, width: 22)
                .BarcodeCode39(40, 1020, "BIN-A42-X", height: 60, showText: true)
                // 2D QR tracking
                .QrCode(560, 950, "https://wms.example.com/pallet/001084692000000001", magnification: 6);

        var palletDoc = zplLabel.Build("PalletLabel-SSCC18", "PALLET-SSCC-001");
        Console.WriteLine($"    ✔ Industrial Pallet Shipping Label generated: {palletDoc.GetBytes().Length} bytes");

        Console.WriteLine("\n✔ Level 03 completed successfully.\n");
        return Task.CompletedTask;
    }
}
