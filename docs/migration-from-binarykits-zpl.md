# Migration Guide from BinaryKits.Zpl to EricksonLopez.Printing.Zpl

> **Navigation:** [⬅️ Migration from ESCPOS_NET](migration-from-escpos-net.md) | [Documentation Index](README.md) | [Next: ADR Catalog ➡️](adr/README.md)

---

This guide details the migration pathway from `BinaryKits.Zpl.Viewer / Protocol` to the high-performance **`EricksonLopez.Printing.Zpl`** ecosystem.

---

## 1. Architectural Comparison

| Dimension | BinaryKits.Zpl | EricksonLopez.Printing.Zpl |
|---|---|---|
| **API Paradigm** | Heavyweight object-tree AST (`new ZplBarcode128(...)`) | Fluent allocation-free builder (`ZplBuilder`) |
| **Network Transport** | Not included (requires manual custom socket wrappers) | Integrated: `TcpPrinterClient`, `SerialPrinterClient` |
| **Native AOT** | Incompatible with strict AOT (reflection in Viewer) | 100% Native AOT without external native libraries |
| **Font Typing** | Complex class inheritance hierarchies | Strongly typed `ZplFont` enum with raw char fallback |
| **Images & Logos** | Mandatory heavy SkiaSharp dependency | Decoupled pure C# satellite package `Zpl.Imaging` |

---

## 2. Code Equivalences

### 2.1. Label Composition
#### Before (BinaryKits.Zpl)
```csharp
var elements = new List<ZplElementBase>
{
    new ZplGraphicBox(20, 20, 400, 200, 2),
    new ZplFieldData(40, 40, "ZEBRA SHIPPING LABEL"),
    new ZplBarcode128(40, 100, "123456", 50)
};

var drawer = new ZplElementDrawer();
var zplString = drawer.Draw(elements);
```

#### After (EricksonLopez.Printing.Zpl)
```csharp
var doc = new ZplBuilder()
    .Box(20, 20, 400, 200, 2)
    .Text(40, 40, "ZEBRA SHIPPING LABEL", ZplFont.Font0)
    .BarcodeCode128(40, 100, "123456", 50)
    .Build("ShippingLabel");
```

---

### 2.2. Transmission to Zebra Hardware
#### Before (BinaryKits.Zpl)
Transport was not provided; developers wrote bespoke socket loops:
```csharp
using var client = new TcpClient("192.168.1.150", 9100);
using var stream = client.GetStream();
var bytes = Encoding.UTF8.GetBytes(zplString);
stream.Write(bytes, 0, bytes.Length);
```

#### After (EricksonLopez.Printing)
```csharp
// High-performance driver with AOT safety, timeouts, cancellation tokens, and Result pattern
var client = new TcpPrinterClient(new TcpPrinterClientOptions
{
    Host = "192.168.1.150",
    Port = 9100
});

var result = await client.PrintAsync(doc);
```

---

> **Navigation:** [⬅️ Migration from ESCPOS_NET](migration-from-escpos-net.md) | [Documentation Index](README.md) | [Next: ADR Catalog ➡️](adr/README.md)
