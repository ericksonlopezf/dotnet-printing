<!-- Copyright © Erickson Lopez. MIT License. -->
# Quick Start Guide — EricksonLopez.Printing

This guide allows you to compile and transmit your first print document in under 5 minutes using the `EricksonLopez.Printing` library ecosystem.

---

## 1. Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0), .NET 9.0, or .NET 10.0 SDK.
- A physical thermal receipt printer (ESC/POS) or industrial label printer (Zebra ZPL II) reachable over raw TCP (port 9100), an RS-232 serial COM port, or a local socket simulator.

---

## 2. Package Installation

Install only the packages required for your specific printing domain:

```bash
# Core abstractions and raw TCP socket transport client
dotnet add package EricksonLopez.Printing

# Fluent builder for Point-of-Sale thermal receipt printers (ESC/POS)
dotnet add package EricksonLopez.Printing.EscPos

# Fluent generator for Zebra industrial barcode labels (ZPL II)
dotnet add package EricksonLopez.Printing.Zpl
```

Optional satellites:
```bash
# Pure C# monochrome dithering & raster image conversion for ESC/POS
dotnet add package EricksonLopez.Printing.EscPos.Imaging

# Pure C# monochrome conversion to Zebra ^GF graphic fields
dotnet add package EricksonLopez.Printing.Zpl.Imaging

# Real-time hardware status queries and DLE EOT sensor telemetry
dotnet add package EricksonLopez.Printing.EscPos.Status

# RS-232 / Virtual COM port serial driver
dotnet add package EricksonLopez.Printing.Serial
```

---

## 3. Example 1: Printing a Thermal Receipt (ESC/POS)

The following example compiles a formatted POS receipt directly in memory using `EscPosBuilder` and transmits it to a network printer over TCP port 9100 without throwing unhandled runtime exceptions:

```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Result;

// 1. Configure the raw TCP network client
var options = new TcpPrinterClientOptions
{
    Host = "192.168.1.200",
    Port = 9100,
    TimeoutMs = 3000
};

var client = new TcpPrinterClient(options);

// 2. Compose the ESC/POS receipt using the fluent builder
using var builder = new EscPosBuilder();
builder
    .Initialize()
    .Align(EscPosAlignment.Center)
    .Bold(true)
    .Line("CENTRAL COFFEE SHOP")
    .Bold(false)
    .Line("123 Main St - Tel: 555-0199")
    .Line("--------------------------------")
    .Align(EscPosAlignment.Left)
    .Line("1x Espresso Coffee         $2.50")
    .Line("1x Butter Croissant        $3.00")
    .Line("--------------------------------")
    .Align(EscPosAlignment.Right)
    .Bold(true)
    .Line("TOTAL: $5.50")
    .Bold(false)
    .Align(EscPosAlignment.Center)
    .Feed(1)
    .QrCode("https://invoice.centralcoffee.com/001-9281")
    .Feed(2)
    .Cut(); // Full cut; use Cut(partial: true) for partial cut

// 3. Compile to an immutable print document
IPrintDocument document = builder.Build("Receipt-001");

// 4. Transmit with exception-free Result pattern handling
Result<bool> result = await client.PrintAsync(document);

if (result.IsSuccess)
{
    Console.WriteLine("✔ Receipt successfully printed.");
}
else
{
    Console.WriteLine($"✘ Printing failed: [{result.Error.Code}] {result.Error.Description}");
}
```

---

## 4. Example 2: Printing an Industrial Label (Zebra ZPL II)

For warehouse logistics, shipping pallets, and manufacturing barcode labels:

```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.Zpl;
using EricksonLopez.Result;

var options = new TcpPrinterClientOptions
{
    Host = "192.168.1.201",
    Port = 9100
};

var client = new TcpPrinterClient(options);

// 1. Assemble the logistics label via fluent ZplBuilder
var builder = new ZplBuilder();
builder
    .Box(x: 30, y: 30, width: 740, height: 440, borderThickness: 3)
    .Text(x: 50, y: 60, text: "CENTRAL WAREHOUSE - INBOUND PALLET", height: 40, width: 35, font: (char)ZplFont.Font0)
    .Text(x: 50, y: 120, text: "DESTINATION: SECTOR B - SHELF 14", height: 25, width: 22, font: (char)ZplFont.FontD)
    .BarcodeCode128(x: 50, y: 180, data: "PAL-88301928", height: 80)
    .Text(x: 50, y: 300, text: "DATE: 2026-09-23 | OPERATOR: E. LOPEZ", height: 20, width: 18, font: (char)ZplFont.FontA);

// 2. Compile to an immutable print document (appends ^XZ terminator)
IPrintDocument labelDoc = builder.Build("Pallet-Label-8830");

// 3. Transmit to Zebra hardware
Result<bool> printResult = await client.PrintAsync(labelDoc);

if (printResult.IsSuccess)
{
    Console.WriteLine("✔ Pallet label transmitted to Zebra printhead.");
}
```

---

## 5. Dependency Injection in ASP.NET Core

In ASP.NET Core web applications, Minimal APIs, or Background Worker Services, register printers in `Program.cs`:

```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;

var builder = WebApplication.CreateBuilder(args);

// Register TCP printer client as Singleton in DI container
builder.Services.AddTcpPrinter(options =>
{
    options.Host = builder.Configuration["Printer:Host"] ?? "192.168.1.200";
    options.Port = 9100;
    options.TimeoutMs = 4000;
});

var app = builder.Build();

app.MapPost("/api/tickets", async (IPrinterClient printer, TicketRequest request) =>
{
    using var escpos = new EscPosBuilder();
    escpos.Initialize().Line(request.Text).Feed(2).Cut();
    
    var doc = escpos.Build("WebTicket");
    var result = await printer.PrintAsync(doc);
    
    return result.IsSuccess
        ? Results.Ok(new { Status = "Printed" })
        : Results.Problem(detail: result.Error.Description, statusCode: 503);
});

app.Run();

public record TicketRequest(string Text);
```

---

## 6. Recommended Next Steps

- **[Installation & Configuration Guide](getting-started.md)**: Connection pooling (`PooledTcpPrinterClient`), RS-232 serial ports, and SignalR remote dispatch.
- **[Production Cookbook](cookbook.md)**: 10 complete recipes solving kitchen orders, fiscal tax receipts, and barcode generation.
- **[Showcase Reference Implementation](showcase-guide.md)**: Execute all 11 progressive levels verifying 100% of the public API surface.
- **[Architecture Guide](architecture-guide.md)**: In-depth system diagrams and memory zero-copy pipeline details.
