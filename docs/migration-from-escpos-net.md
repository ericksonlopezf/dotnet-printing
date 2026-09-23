# Migration Guide from ESCPOS_NET to EricksonLopez.Printing

> **Navigation:** [⬅️ Web-to-Edge Guide](web-to-edge-guide.md) | [Documentation Index](README.md) | [Next: Migration from BinaryKits.Zpl ➡️](migration-from-binarykits-zpl.md)

---

This guide assists engineering teams in migrating existing codebases from the legacy `ESCPOS_NET` library to the modern **`EricksonLopez.Printing`** ecosystem.

---

## 1. Architectural Comparison

| Dimension | ESCPOS_NET | EricksonLopez.Printing |
|---|---|---|
| **.NET Runtime** | .NET Standard 2.0 / Legacy Framework | .NET 8 / .NET 9 / .NET 10 Multi-Targeting |
| **Native AOT** | Incompatible (reflection, dynamic dependencies) | 100% Verified Compatible (`IsAotCompatible=true`) |
| **Error Handling** | Uncontrolled runtime exception throwing | Functional `Result<bool>` with canonical error codes |
| **Dependency Injection** | No official Microsoft DI integration | First-class: `services.AddTcpPrinter(...)`, `AddSerialPrinter(...)` |
| **Web-to-Edge Cloud Dispatch** | Not available | Out-of-the-box: `EricksonLopez.Printing.SignalR` |
| **Resilience / Retries** | Manual custom loops | Composable decorator: `client.WithRetry(...)` |
| **Observability** | Ad-hoc unformatted logging | Structured telemetry with standard Event IDs and `ILogger<T>` |

---

## 2. Code Equivalences

### 2.1. Dependency Injection Configuration
#### Before (ESCPOS_NET)
```csharp
// Manual registration
services.AddSingleton<IPrinter>(new ImmediateNetworkPrinter(new ImmediateNetworkPrinterSettings
{
    ConnectionString = "192.168.1.100:9100"
}));
```

#### After (EricksonLopez.Printing)
```csharp
// Idiomatic registration with options and automatic telemetry injection
services.AddTcpPrinter(options =>
{
    options.Host = "192.168.1.100";
    options.Port = 9100;
    options.TimeoutMs = 3000;
});
```

---

### 2.2. Receipt Document Composition
#### Before (ESCPOS_NET)
```csharp
var emitter = new EPSON();
byte[] bytes = ByteSplicer.Combine(
    emitter.Initialize(),
    emitter.CenterAlign(),
    emitter.PrintLine("STORE NAME"),
    emitter.FeedLines(2),
    emitter.FullCut()
);
```

#### After (EricksonLopez.Printing)
```csharp
using var builder = new EscPosBuilder();
var doc = builder
    .Initialize()
    .Align(EscPosAlignment.Center)
    .Line("STORE NAME")
    .Feed(2)
    .Cut()
    .Build("ReceiptDocument");
```

---

### 2.3. Network Transmission and Error Handling
#### Before (ESCPOS_NET)
```csharp
try
{
    printer.Write(bytes);
}
catch (SocketException ex)
{
    // Exception-based error handling
}
```

#### After (EricksonLopez.Printing)
```csharp
var result = await printerClient.PrintAsync(doc);

if (result.IsFailure)
{
    logger.LogError("Print failed: [{Code}] {Description}", result.Error.Code, result.Error.Description);
}
```

---

> **Navigation:** [⬅️ Web-to-Edge Guide](web-to-edge-guide.md) | [Documentation Index](README.md) | [Next: Migration from BinaryKits.Zpl ➡️](migration-from-binarykits-zpl.md)
