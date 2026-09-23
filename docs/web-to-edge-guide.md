# Architecture & Implementation Guide: Web-to-Edge Printing with SignalR

> **Navigation:** [⬅️ ZPL II Reference](zpl-command-reference.md) | [Documentation Index](README.md) | [Next: Migration from ESCPOS_NET ➡️](migration-from-escpos-net.md)

---

This guide details the implementation of an end-to-end distributed architecture for **remote printing from cloud backends to on-premise physical printers** using **`EricksonLopez.Printing.SignalR`**.

---

## 1. The Challenge: Printing in Cloud SaaS Architectures

In web applications and multi-tenant SaaS platforms hosted in the cloud (Azure, AWS, GCP, or Kubernetes), cloud servers lack direct network access to customer on-premise local area networks (LANs) where physical printers reside (point-of-sale receipt printers, kitchen order printers, Zebra warehouse barcode labelers) due to firewalls, corporate NATs, and non-routable private IP subnets.

Traditionally, addressing this required:
- Complex, fragile site-to-site or client VPN tunnels for every store or warehouse branch.
- Insecure router port-forwarding exposing raw printer sockets (port 9100) to the public Internet.
- Generating client-side PDFs requiring manual user clicks through the browser print dialog, disrupting cashier workflows.

---

## 2. The Solution: Edge Agents with Reverse SignalR Tunnel

With `EricksonLopez.Printing.SignalR`, a lightweight .NET background worker (compiled as a self-contained Native AOT binary) runs within the client local network, opens an outbound WebSocket/SignalR connection to the cloud backend, and registers to its assigned printer group. When the cloud backend compiles a receipt or label, it dispatches the payload instantly to the edge agent, which transmits it directly to the physical printer over raw TCP socket or Serial COM port.

```
┌─────────────────────────────────────────────────────────────┐
│                   CLOUD BACKEND (ASP.NET Core)              │
│                                                             │
│   [SaaS API Controller / Background Service]                │
│                 │                                           │
│                 ▼                                           │
│   [IHubPrinterDispatcher]                                   │
│                 │                                           │
│                 ▼                                           │
│            [PrinterHub]                                     │
└──────────────────┬──────────────────────────────────────────┘
                   │  Outbound WebSocket / SignalR Connection
                   │  (Group: "printer:POS-01")
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                 LOCAL NETWORK / EDGE AGENT                  │
│                                                             │
│   [HubConnection (C# Edge Daemon / Native AOT)]             │
│                 │                                           │
│                 ▼                                           │
│   [TcpPrinterClient / SerialPrinterClient]                  │
│                 │                                           │
│                 ▼                                           │
│    [Physical Epson / Zebra Printer]                         │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Cloud Backend Implementation (ASP.NET Core Server)

### 3.1. Service Registration
In `Program.cs`:

```csharp
using EricksonLopez.Printing.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Register PrinterHub and IHubPrinterDispatcher
builder.Services.AddPrintingSignalR();

var app = builder.Build();

// Map the typed SignalR Hub endpoint
app.MapHub<PrinterHub>("/hubs/printer");

app.Run();
```

### 3.2. Dispatching a Print Job from an API Controller
```csharp
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.SignalR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/checkout")]
public class CheckoutController : ControllerBase
{
    private readonly IHubPrinterDispatcher _dispatcher;

    public CheckoutController(IHubPrinterDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteSale([FromBody] SaleRequest request)
    {
        // 1. Compile native ESC/POS receipt bytecode
        using var builder = new EscPosBuilder();
        var receipt = builder
            .Initialize()
            .Align(EscPosAlignment.Center)
            .Bold(true)
            .Line("ENTERPRISE SAAS STORE")
            .Bold(false)
            .Divider()
            .TableRow("1x Margherita Pizza", "$12.50")
            .TableRow("2x Beverages", "$5.00")
            .Divider()
            .Bold(true)
            .TableRow("TOTAL", "$17.50")
            .Bold(false)
            .Feed(3)
            .Cut()
            .Build("SaleReceipt");

        // 2. Dispatch to the edge client registered for printer 'POS-01'
        var result = await _dispatcher.DispatchAsync("POS-01", receipt);

        if (result.IsFailure)
        {
            return StatusCode(503, new { error = result.Error.Description });
        }

        return Ok(new { status = "PrintedSuccessfully" });
    }
}
```

---

## 4. Local Edge Agent Implementation (C# Worker Client)

The local edge client is a lightweight console daemon that can run as a Windows Service or Linux `systemd` daemon:

```csharp
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using EricksonLopez.Printing;
using EricksonLopez.Printing.SignalR;

const string serverHubUrl = "https://your-cloud-saas.com/hubs/printer";
const string printerName = "POS-01";

// 1. Configure local hardware transport client (TCP or Serial COM)
var printerClient = new TcpPrinterClient(new TcpPrinterClientOptions
{
    Host = "192.168.1.200", // Local static IP of the thermal receipt printer
    Port = 9100,
    TimeoutMs = 3000
});

// 2. Connect to cloud SignalR Hub
var connection = new HubConnectionBuilder()
    .WithUrl(serverHubUrl)
    .WithAutomaticReconnect()
    .Build();

// 3. Subscribe to print jobs dispatched to this physical printer
connection.On<PrintJobMessage>(nameof(IPrinterHubClient.OnPrintJobReceived), async message =>
{
    Console.WriteLine($"[Job Received] ID: {message.JobId} - Doc: {message.DocumentName}");

    var rawBytes = Convert.FromBase64String(message.PayloadBase64);
    var doc = new RawPrintDocument(rawBytes, message.DocumentName);

    var printResult = await printerClient.PrintAsync(doc);
    if (printResult.IsSuccess)
    {
        Console.WriteLine($"[Print Succeeded] {message.JobId}");
    }
    else
    {
        Console.WriteLine($"[Hardware Error] {printResult.Error.Description}");
    }
});

await connection.StartAsync();

// 4. Register printer group with the Hub
await connection.InvokeAsync(nameof(PrinterHub.RegisterPrinter), printerName);
Console.WriteLine($"Edge Agent connected and listening for '{printerName}'. Press Ctrl+C to terminate.");

await Task.Delay(-1);
```

---

> **Navigation:** [⬅️ ZPL II Reference](zpl-command-reference.md) | [Documentation Index](README.md) | [Next: Migration from ESCPOS_NET ➡️](migration-from-escpos-net.md)
