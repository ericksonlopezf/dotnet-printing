<!-- Copyright © Erickson Lopez. MIT License. -->
# Production Cookbook — EricksonLopez.Printing

This cookbook contains complete, tested, and compilable solutions for common thermal receipt and industrial label printing requirements in production, derived exclusively from the public API of `EricksonLopez.Printing`.

---

## Recipe Index

1. [Recipe 1: High-Volume POS Printing with Persistent Socket Pooling](#recipe-1-high-volume-pos-printing-with-persistent-socket-pooling)
2. [Recipe 2: Complete Retail Fiscal Receipt with Taxes, Drawer Kick, and Cutting](#recipe-2-complete-retail-fiscal-receipt-with-taxes-drawer-kick-and-cutting)
3. [Recipe 3: Industrial Logistics Pallet Label in ZPL II with GS1-128 and QR](#recipe-3-industrial-logistics-pallet-label-in-zpl-ii-with-gs1-128-and-qr)
4. [Recipe 4: Monochrome BMP Logo Printing with Floyd-Steinberg Dithering](#recipe-4-monochrome-bmp-logo-printing-with-floyd-steinberg-dithering)
5. [Recipe 5: RS-232 / Virtual COM Port Serial Printing with Concurrency Safety](#recipe-5-rs-232--virtual-com-port-serial-printing-with-concurrency-safety)
6. [Recipe 6: Resilience Retries with Physical Idempotency Protection](#recipe-6-resilience-retries-with-physical-idempotency-protection)
7. [Recipe 7: Distributed Cloud-to-Edge Dispatch with ASP.NET Core SignalR](#recipe-7-distributed-cloud-to-edge-dispatch-with-aspnet-core-signalr)
8. [Recipe 8: Real-Time Hardware Readiness Auditing with DLE EOT Telemetry](#recipe-8-real-time-hardware-readiness-auditing-with-dle-eot-telemetry)
9. [Recipe 9: Multi-Printer Dependency Injection with Keyed Services](#recipe-9-multi-printer-dependency-injection-with-keyed-services)
10. [Recipe 10: Preventing ZPL Command Injection in Dynamic Untrusted Input](#recipe-10-preventing-zpl-command-injection-in-dynamic-untrusted-input)

---

## Recipe 1: High-Volume POS Printing with Persistent Socket Pooling

### Problem
In high-throughput retail checkout environments (supermarkets, fast food), opening and closing raw TCP sockets (`new TcpClient()`) for every transaction introduces connection latency (TCP 3-way handshake) and causes socket exhaustion under heavy load (`TIME_WAIT` port exhaustion).

### Solution
Use `PooledTcpPrinterClient`. It maintains a persistent TCP socket across print jobs and serializes hardware transmission using an internal `SemaphoreSlim(1, 1)`, reconnecting automatically if the printer drops offline.

### Complete Code
```csharp
using System;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;

public class HighVolumePrintingService : IAsyncDisposable
{
    private readonly PooledTcpPrinterClient _client;

    public HighVolumePrintingService(string printerIp, int port = 9100)
    {
        var options = new TcpPrinterClientOptions
        {
            Host = printerIp,
            Port = port,
            TimeoutMs = 3000,
            NoDelay = true,      // Disable Nagle algorithm for instant packet dispatch
            LingerSeconds = 0    // Immediately release socket resources upon teardown
        };

        _client = new PooledTcpPrinterClient(options);
    }

    public async Task PrintFastTicketAsync(string orderId, decimal total)
    {
        using var builder = new EscPosBuilder();
        builder.Initialize()
               .Align(EscPosAlignment.Center)
               .Bold(true)
               .Line("EXPRESS CHECKOUT")
               .Bold(false)
               .TableRow($"Order #{orderId}", $"${total:F2}", 32)
               .Feed(2)
               .Cut();

        var document = builder.Build($"Ticket-{orderId}");
        var result = await _client.PrintAsync(document);

        if (result.IsFailure)
        {
            Console.WriteLine($"[Error] Print failed for order {orderId}: [{result.Error.Code}] {result.Error.Description}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
```

### Explanation
- `NoDelay = true` instructs the TCP stack to transmit packets immediately without buffering.
- If the printer is power-cycled, `PooledTcpPrinterClient.PrintAsync` detects the closed socket via non-blocking polling (`Socket.Poll`), recycles the connection, and re-establishes socket connectivity transparently.

---

## Recipe 2: Complete Retail Fiscal Receipt with Taxes, Drawer Kick, and Cutting

### Problem
Generate a full commercial retail receipt including centered header, justified line items, QR code for tax verification, electrical cash drawer pulse, and partial paper cut.

### Solution
Use `EscPosBuilder` chaining `.Align()`, `.TableRow()`, `.Divider()`, `.QrCode()`, `.OpenCashDrawer()`, and `.Cut()`.

### Complete Code
```csharp
using System;
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;

public static class FiscalReceiptFactory
{
    public static IPrintDocument CreateReceipt(string invoiceNumber, (string Item, decimal Price)[] items, decimal taxRate)
    {
        using var builder = new EscPosBuilder();

        builder.Initialize()
               .Align(EscPosAlignment.Center)
               .Bold(true)
               .FontSize(widthMultiplier: 2, heightMultiplier: 2)
               .Line("NEAPOLITAN PIZZERIA")
               .FontSize(widthMultiplier: 1, heightMultiplier: 1)
               .Bold(false)
               .Line("VAT ID: US-12345678")
               .Line("123 Main Street, Suite 4B")
               .Divider(42, '=')
               .Align(EscPosAlignment.Left);

        decimal subtotal = 0;
        foreach (var (item, price) in items)
        {
            builder.TableRow(item, $"${price:F2}", 42);
            subtotal += price;
        }

        var tax = subtotal * taxRate;
        var total = subtotal + tax;

        builder.Divider(42, '-')
               .TableRow("Subtotal", $"${subtotal:F2}", 42)
               .TableRow($"Sales Tax ({(taxRate * 100):F0}%)", $"${tax:F2}", 42)
               .Divider(42, '=')
               .Bold(true)
               .Underline(EscPosUnderline.DoubleDot)
               .TableRow("TOTAL DUE", $"${total:F2}", 42)
               .Underline(EscPosUnderline.None)
               .Bold(false)
               .Feed(1)
               .Align(EscPosAlignment.Center)
               .Line("Scan to verify fiscal invoice:")
               .QrCode($"https://fiscal.example.com/verify?num={invoiceNumber}", moduleSize: 5, errorCorrection: EscPosQrErrorCorrection.M)
               .Feed(1)
               .Line("Thank you for your business!")
               .OpenCashDrawer() // Triggers 24V electrical pulse to RJ11 cash drawer solenoid
               .Feed(3)          // Feeds paper past the cutter blade
               .Cut(partial: false);

        return builder.Build($"Invoice-{invoiceNumber}", idempotencyKey: invoiceNumber);
    }
}
```

### Explanation
- `TableRow(left, right, totalWidth)` dynamically computes space padding between left and right strings.
- `OpenCashDrawer()` emits the standard `ESC p 0 25 250` electrical pulse to the cash drawer solenoid.
- Feeding paper before cutting (`Feed(3)`) ensures text is not sliced off by the physical cutter position.

---

## Recipe 3: Industrial Logistics Pallet Label in ZPL II with GS1-128 and QR

### Problem
Compose industrial warehouse pallet tags compliant with GS1 standards (SSCC-18), barcode scanning by forklift operators, digital customs QR code, and divider boxes.

### Solution
Use `ZplBuilder` with vector drawing primitives (`Box`, `Circle`) and barcode methods (`BarcodeCode128`, `BarcodeEan13`, `QrCode`).

### Complete Code
```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.Zpl;

public static class PalletLabelGenerator
{
    public static IPrintDocument GenerateSsccLabel(string sscc18, string destinationHub, string gtin)
    {
        var zpl = new ZplBuilder();

        // Outer border
        zpl.Box(x: 20, y: 20, width: 760, height: 1160, borderThickness: 4)
           // Horizontal dividers
           .Box(x: 20, y: 160, width: 760, height: 4, borderThickness: 4)
           .Box(x: 20, y: 500, width: 760, height: 4, borderThickness: 4)
           // Origin header
           .Text(40, 40, "ORIGIN: CENTRAL LOGISTICS HUB", ZplFont.Font0, height: 25, width: 25)
           .Text(40, 75, "CARRIER: TRANS-EXPRESS FREIGHT", ZplFont.Font0, height: 25, width: 25)
           .Text(40, 110, "LOAD TYPE: NON-PERISHABLE FOOD", ZplFont.Font0, height: 22, width: 22)
           // Destination
           .Text(40, 180, "FINAL DESTINATION:", ZplFont.Font0, height: 25, width: 25)
           .Text(40, 220, destinationHub, ZplFont.Font0, height: 45, width: 45)
           // Inspection stamp
           .Circle(x: 640, y: 220, diameter: 100, borderThickness: 3)
           .Text(660, 255, "QC OK", ZplFont.Font0, height: 25, width: 25)
           // GS1-128 SSCC barcode
           .Text(40, 520, "SSCC (00):", ZplFont.Font0, height: 22, width: 22)
           .BarcodeCode128(x: 40, y: 560, data: sscc18, height: 120, showText: true)
           // Master GTIN-13
           .Text(40, 740, "MASTER GTIN-13:", ZplFont.Font0, height: 22, width: 22)
           .BarcodeEan13(x: 40, y: 775, data: gtin, height: 75, showText: true)
           // Digital customs QR code
           .Text(480, 740, "DIGITAL EXPEDITION:", ZplFont.Font0, height: 20, width: 20)
           .QrCode(x: 520, y: 780, data: $"https://wms.enterprise.com/sscc/{sscc18}", magnification: 6);

        return zpl.Build($"Pallet-{sscc18}", idempotencyKey: sscc18);
    }
}
```

---

## Recipe 4: Monochrome BMP Logo Printing with Floyd-Steinberg Dithering

### Problem
Print a business logo on an 80mm thermal receipt printer from a 24-bit uncompressed BMP file with smooth gradients and no unmanaged C++ graphics dependencies.

### Solution
Use `EricksonLopez.Printing.EscPos.Imaging` with `EscPosDitherAlgorithm.FloydSteinberg`, configuring `EscPosBuilder` with `safeMode: false`.

### Complete Code
```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.EscPos.Imaging;

public static class BrandedReceiptPrinter
{
    public static IPrintDocument CreateBrandedReceipt(byte[] logoBmpBytes, string storeName)
    {
        // safeMode: false is REQUIRED to permit binary raster command streams (GS v 0)
        using var builder = new EscPosBuilder(safeMode: false);

        builder.Initialize()
               .Align(EscPosAlignment.Center)
               .Image(logoBmpBytes, EscPosDitherAlgorithm.FloydSteinberg, scale: 0)
               .Feed(1)
               .Bold(true)
               .Line(storeName)
               .Bold(false)
               .Line("Welcome to our flagship store")
               .Divider(32, '-')
               .Line("Demonstration Purchase Ticket")
               .Feed(2)
               .Cut();

        return builder.Build("BrandedReceipt");
    }
}
```

### Explanation
- The Floyd-Steinberg algorithm diffuses quantization errors into neighboring pixels, rendering grayscale appearances on monochrome thermal printheads.
- The converter rents buffers from `ArrayPool<byte>.Shared`, eliminating LOH allocations.

---

## Recipe 5: RS-232 / Virtual COM Port Serial Printing with Concurrency Safety

### Problem
Physical RS-232 COM ports are single-channel devices. If concurrent threads write to a serial port simultaneously, bytes interleave and corrupt print jobs.

### Solution
Use `SerialPrinterClient` from `EricksonLopez.Printing.Serial`. It wraps the port and serializes all transmissions using `SemaphoreSlim(1, 1)`.

### Complete Code
```csharp
using System;
using System.IO.Ports;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.Serial;

public class SerialPosPrinterService : IDisposable
{
    private readonly SerialPrinterClient _serialClient;

    public SerialPosPrinterService(string comPort = "COM1", int baudRate = 115200)
    {
        var options = new SerialPrinterClientOptions
        {
            PortName = comPort,
            BaudRate = baudRate,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.RequestToSend, // Hardware flow control (RTS/CTS)
            WriteTimeoutMs = 3000,
            ReadTimeoutMs = 1000
        };

        _serialClient = new SerialPrinterClient(options);
    }

    public async Task<bool> PrintDocumentAsync(IPrintDocument document)
    {
        var result = await _serialClient.PrintAsync(document);
        if (result.IsFailure)
        {
            Console.WriteLine($"[Serial Error] [{result.Error.Code}] {result.Error.Description}");
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _serialClient.Dispose();
    }
}
```

---

## Recipe 6: Resilience Retries with Physical Idempotency Protection

### Problem
Wi-Fi socket drops are common in warehouses and restaurants. However, if a socket disconnects mid-stream while paper was actively advancing, blindly retrying duplicates tickets.

### Solution
Decorate the client with `ResilientPrinterClient` via `.WithRetry()`, adhering to the Golden Rule of Physical Idempotency: non-idempotent documents abort immediately on mid-stream errors (`RetryOnTransmissionError = false`).

### Complete Code
```csharp
using System;
using System.Threading.Tasks;
using EricksonLopez.Printing;

public class PrintingPipelineFactory
{
    public static IPrinterClient CreateResilientPrinter(string printerIp)
    {
        var tcpOptions = new TcpPrinterClientOptions
        {
            Host = printerIp,
            Port = 9100,
            TimeoutMs = 4000
        };

        var baseClient = new PooledTcpPrinterClient(tcpOptions);

        return baseClient.WithRetry(retryOptions =>
        {
            retryOptions.MaxRetries = 3;
            retryOptions.InitialDelay = TimeSpan.FromMilliseconds(200);
            retryOptions.MaxDelay = TimeSpan.FromSeconds(2);
            retryOptions.BackoffMultiplier = 2.0;
            retryOptions.RetryOnTransmissionError = false; // Never duplicate physical paper
        });
    }

    public static async Task ExecutePrintJob(IPrinterClient client, IPrintDocument document)
    {
        var result = await client.PrintAsync(document);
        if (result.IsSuccess)
        {
            Console.WriteLine($"✔ Job '{document.DocumentName}' printed successfully.");
        }
        else
        {
            Console.WriteLine($"❌ Final error after retries: [{result.Error.Code}] {result.Error.Description}");
        }
    }
}
```

---

## Recipe 7: Distributed Cloud-to-Edge Dispatch with ASP.NET Core SignalR

### Problem
A centralized cloud SaaS backend (AWS/Azure) needs to dispatch print jobs to branch store printers residing behind corporate NAT firewalls without VPN tunnels.

### Solution
Use `EricksonLopez.Printing.SignalR`. The cloud server hosts `PrinterHub` and dispatches via `HubPrinterDispatcher`. The local store agent maintains an outbound persistent WebSocket and implements `IPrinterHubClient`.

### Complete Code

#### Cloud SaaS Backend
```csharp
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.SignalR;

public class CloudCheckoutService
{
    private readonly IHubPrinterDispatcher _dispatcher;

    public CloudCheckoutService(IHubPrinterDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<bool> DispatchOrderToStoreAsync(string storePrinterId, IPrintDocument receipt)
    {
        var result = await _dispatcher.DispatchAsync(storePrinterId, receipt);
        return result.IsSuccess;
    }
}
```

#### Edge Store Agent (Branch Worker Daemon)
```csharp
using System;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.SignalR;

public class EdgePrintingDaemon : IPrinterHubClient
{
    private readonly IPrinterClient _localHardwarePrinter;

    public EdgePrintingDaemon(IPrinterClient localHardwarePrinter)
    {
        _localHardwarePrinter = localHardwarePrinter;
    }

    public async Task OnPrintJobReceived(PrintJobMessage job)
    {
        Console.WriteLine($"[Edge] Received print job {job.JobId} for local printer {job.PrinterName}");

        byte[] rawCommands = Convert.FromBase64String(job.PayloadBase64);
        var document = new RawPrintDocument(rawCommands, job.DocumentName, job.JobId, isIdempotent: true);

        var result = await _localHardwarePrinter.PrintAsync(document);
        if (result.IsFailure)
        {
            Console.WriteLine($"[Edge Error] Local printing failed: {result.Error.Description}");
        }
    }
}
```

---

## Recipe 8: Real-Time Hardware Readiness Auditing with DLE EOT Telemetry

### Problem
Verify that an ESC/POS printer is online, the cover is closed, paper is loaded, and the cutter is operational before initiating print jobs.

### Solution
Send `DLE EOT 1..4` real-time opcodes using `EscPosStatusCommands` and parse the 4-byte response using `EscPosStatusParser`.

### Complete Code
```csharp
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using EricksonLopez.Printing.EscPos.Status;

public static class PrinterHealthChecker
{
    public static async Task<EscPosPrinterStatus> QueryStatusOverTcpAsync(string ip, int port = 9100)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(ip, port);
        using var stream = client.GetStream();

        // Transmit DLE EOT 1, 2, 3, and 4 real-time queries
        await stream.WriteAsync(EscPosStatusCommands.QueryPrinterStatus);
        await stream.WriteAsync(EscPosStatusCommands.QueryOfflineCause);
        await stream.WriteAsync(EscPosStatusCommands.QueryErrorStatus);
        await stream.WriteAsync(EscPosStatusCommands.QueryPaperSensor);
        await stream.FlushAsync();

        // Read the 4 response bytes
        var responseBuffer = new byte[4];
        var bytesRead = 0;
        while (bytesRead < 4)
        {
            var read = await stream.ReadAsync(responseBuffer.AsMemory(bytesRead, 4 - bytesRead));
            if (read == 0) break;
            bytesRead += read;
        }

        if (bytesRead < 4)
        {
            throw new InvalidOperationException("Printer failed to return 4 status bytes.");
        }

        return EscPosStatusParser.Parse(
            responseBuffer[0],
            responseBuffer[1],
            responseBuffer[2],
            responseBuffer[3]);
    }
}
```

---

## Recipe 9: Multi-Printer Dependency Injection with Keyed Services

### Problem
A single POS application needs to interact with three distinct printers: cash register receipt printer, kitchen order printer, and serial ticket printer.

### Solution
Register keyed services via `AddKeyedPooledTcpPrinter`, `AddKeyedTcpPrinter`, and `AddKeyedSerialPrinter`.

### Complete Code
```csharp
using System.Threading.Tasks;
using EricksonLopez.Printing;
using Microsoft.Extensions.DependencyInjection;

public static class MultiPrinterConfiguration
{
    public static void Configure(IServiceCollection services)
    {
        services.AddKeyedPooledTcpPrinter("CashRegister", opt =>
        {
            opt.Host = "192.168.1.101";
            opt.Port = 9100;
        });

        services.AddKeyedTcpPrinter("Kitchen", opt =>
        {
            opt.Host = "192.168.1.102";
            opt.Port = 9100;
        });

        services.AddKeyedSerialPrinter("Fiscal", opt =>
        {
            opt.PortName = "COM2";
            opt.BaudRate = 115200;
        });
    }
}

public class MultiPrinterOrderService
{
    private readonly IPrinterClient _receiptPrinter;
    private readonly IPrinterClient _kitchenPrinter;

    public MultiPrinterOrderService(
        [FromKeyedServices("CashRegister")] IPrinterClient receiptPrinter,
        [FromKeyedServices("Kitchen")] IPrinterClient kitchenPrinter)
    {
        _receiptPrinter = receiptPrinter;
        _kitchenPrinter = kitchenPrinter;
    }

    public async Task ProcessOrderAsync(IPrintDocument receiptDoc, IPrintDocument kitchenDoc)
    {
        var taskReceipt = _receiptPrinter.PrintAsync(receiptDoc);
        var taskKitchen = _kitchenPrinter.PrintAsync(kitchenDoc);

        await Task.WhenAll(taskReceipt, taskKitchen);
    }
}
```

---

## Recipe 10: Preventing ZPL Command Injection in Dynamic Untrusted Input

### Problem
If dynamic user strings containing `^` or `~` are concatenated into ZPL labels, an attacker could inject formatting commands altering printer calibration or printing unexpected content.

### Solution
Use `ZplBuilder` with `safeMode: true`. Control delimiters are automatically sanitized using `^FH_` hexadecimal escaping (`^` as `_5E`, `~` as `_7E`).

### Complete Code
```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.Zpl;

public static class SecureZplLabelFactory
{
    public static IPrintDocument BuildCustomerBadge(string untrustedName, string untrustedCompany)
    {
        // safeMode: true activates strict ZPL command injection defenses
        var zpl = new ZplBuilder(safeMode: true);

        zpl.Box(30, 30, 600, 400, borderThickness: 3)
           .Text(50, 60, "VISITOR BADGE", ZplFont.Font0, height: 35, width: 35)
           // Even if untrustedName contains "^FS^XZ", ZplBuilder escapes it safely
           .Text(50, 130, untrustedName, ZplFont.Font0, height: 30, width: 30)
           .Text(50, 180, untrustedCompany, ZplFont.FontD, height: 20, width: 20)
           .BarcodeCode128(50, 240, "VIS-2026-991", height: 70, showText: true);

        return zpl.Build("VisitorBadge");
    }
}
```
