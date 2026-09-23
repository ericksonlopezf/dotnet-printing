<!-- Copyright © Erickson Lopez. MIT License. -->
# Installation & Configuration Guide — EricksonLopez.Printing

This guide details how to integrate `EricksonLopez.Printing` into modern .NET applications, configure network and hardware options, register dependency injection lifecycles, and deploy to Docker containers and edge appliances.

---

## 1. NuGet Package Ecosystem

The suite consists of highly cohesive, decoupled packages designed under the principle of least dependency privilege:

| Package | Layer | Core Responsibility |
|---|---|---|
| `EricksonLopez.Printing` | **Core** | Fundamental printing contracts (`IPrinterClient`, `IPrintDocument`), ephemeral TCP client (`TcpPrinterClient`), persistent connection pool (`PooledTcpPrinterClient`), transient retry decorator (`ResilientPrinterClient`), and Microsoft DI extensions. |
| `EricksonLopez.Printing.EscPos` | **Core** | Fluent ESC/POS receipt builder (`EscPosBuilder`), text styling, table formatting, 1D/2D barcodes, cash drawer kick, and paper cutters. |
| `EricksonLopez.Printing.Zpl` | **Core** | Fluent Zebra ZPL II label compiler (`ZplBuilder`), strongly-typed fonts (`ZplFont`), geometric primitives, Code128, and QR codes. |
| `EricksonLopez.Printing.EscPos.Imaging` | **Infrastructure** | Pure managed C# Floyd-Steinberg and Threshold dithering for monochrome ESC/POS raster bit images (`GS v 0`). |
| `EricksonLopez.Printing.Zpl.Imaging` | **Infrastructure** | Pure managed C# monochrome conversion to Zebra Graphic Field (`^GF`) hexadecimal sequences. |
| `EricksonLopez.Printing.EscPos.Status` | **Infrastructure** | Real-time `DLE EOT` hardware telemetry querying and zero-allocation status parsing (paper-out, cover-open, cash drawer). |
| `EricksonLopez.Printing.Serial` | **Infrastructure** | RS-232 / Virtual COM port serial driver based on `System.IO.Ports` with `SemaphoreSlim` serialized thread safety. |
| `EricksonLopez.Printing.SignalR` | **Infrastructure** | Real-time cloud-to-edge hardware dispatch bridge using ASP.NET Core SignalR (`PrinterHub`, `HubPrinterDispatcher`). |

---

## 2. Dependency Injection (DI Patterns)

### 2.1. Standard TCP Printer Client (Ephemeral)
Creates a new TCP socket connection per print job and immediately disposes it upon completion. Ideal for low-frequency shared printers:

```csharp
builder.Services.AddTcpPrinter(options =>
{
    options.Host = "192.168.1.150";
    options.Port = 9100;
    options.TimeoutMs = 5000;
    options.NoDelay = true; // Disable Nagle algorithm for minimal latency
    options.LingerSeconds = 0; // Avoid TIME_WAIT socket accumulation
});
```

### 2.2. Persistent Pooled TCP Client
Maintains an open, reusable TCP socket across multiple print jobs with thread-safe serialized transmission enforced by `SemaphoreSlim(1, 1)`. Highly recommended for high-volume POS checkouts and automated logistics lines:

```csharp
builder.Services.AddPooledTcpPrinter(options =>
{
    options.Host = "192.168.1.150";
    options.Port = 9100;
    options.NoDelay = true;
});
```

### 2.3. Multiple Printers via .NET Keyed Services
When an application routes print jobs to distinct physical hardware (e.g., Kitchen, Bar, and Front Desk), register them using keyed services:

```csharp
// Register Bar printer (ephemeral TCP)
builder.Services.AddKeyedTcpPrinter("bar-printer", options =>
{
    options.Host = "192.168.1.101";
    options.Port = 9100;
});

// Register Kitchen printer (persistent pooled TCP)
builder.Services.AddKeyedPooledTcpPrinter("kitchen-printer", options =>
{
    options.Host = "192.168.1.102";
    options.Port = 9100;
});
```

Consume keyed printers in Minimal APIs or controllers:

```csharp
app.MapPost("/api/orders/{id}/cook", async (
    [FromKeyedServices("kitchen-printer")] IPrinterClient kitchenPrinter,
    int id) =>
{
    using var builder = new EscPosBuilder();
    builder.Initialize().Line($"=== KITCHEN ORDER #{id} ===").Feed(2).Cut();
    
    var doc = builder.Build($"Order-{id}");
    var result = await kitchenPrinter.PrintAsync(doc);
    
    return result.IsSuccess ? Results.Ok() : Results.Problem(result.Error.Description);
});
```

### 2.4. RS-232 / COM Serial Printers
Install `EricksonLopez.Printing.Serial` and register via `AddSerialPrinter`:

```csharp
using System.IO.Ports;
using EricksonLopez.Printing;

builder.Services.AddSerialPrinter(options =>
{
    options.PortName = "COM3"; // Or "/dev/ttyUSB0" on Linux
    options.BaudRate = 9600;
    options.Parity = Parity.None;
    options.DataBits = 8;
    options.StopBits = StopBits.One;
    options.Handshake = Handshake.None;
    options.WriteTimeoutMs = 3000;
});
```

---

## 3. Configuration with `appsettings.json`

Decouple physical hardware network endpoints into configuration files:

```json
{
  "Printing": {
    "ReceiptPrinter": {
      "Host": "192.168.1.100",
      "Port": 9100,
      "TimeoutMs": 4000
    }
  }
}
```

Binding in C#:

```csharp
builder.Services.AddPooledTcpPrinter(options =>
{
    var config = builder.Configuration.GetSection("Printing:ReceiptPrinter");
    options.Host = config["Host"] ?? "192.168.1.100";
    options.Port = int.Parse(config["Port"] ?? "9100");
    options.TimeoutMs = int.Parse(config["TimeoutMs"] ?? "4000");
});
```

---

## 4. Container Deployment (Docker & Linux)

When deploying microservices or edge print daemons in containers:
1. **Network Connectivity**: Ensure container network mode allows outbound TCP access to port 9100 on the printer's local subnet.
2. **Serial Port Passthrough**: If utilizing `EricksonLopez.Printing.Serial` in Linux containers, mount the device node:
   ```yaml
   devices:
     - "/dev/ttyUSB0:/dev/ttyUSB0"
   ```
3. **Native AOT & Trimming**: All core and satellite packages are verified for full trimming (`PublishTrimmed=true`) and Ahead-of-Time compilation (`PublishAot=true`), generating single-file binaries with sub-15ms startup times.
