# ZPL II Command Reference — EricksonLopez.Printing.Zpl

> **Navigation:** [⬅️ ESC/POS Reference](escpos-command-reference.md) | [Documentation Index](README.md) | [Next: SignalR Web-to-Edge Guide ➡️](web-to-edge-guide.md)

---

This guide details all commands and instructions supported by `ZplBuilder` and `Zpl.Imaging` for industrial Zebra label printers.

---

## 1. Format Structure and Escape Hatch

| C# Method | ZPL II Command | Description |
|---|---|---|
| Constructor `new ZplBuilder()` | `^XA` | Opens the label format buffer |
| `Raw(string rawZpl)` | Arbitrary | Injects custom raw ZPL command sequences directly |
| `Build(documentName)` | `^XZ` | Closes label format and compiles an immutable `IPrintDocument` |

---

## 2. Text Fields and Typed Typography

```csharp
var builder = new ZplBuilder();

// Using strongly typed font enum (ZplFont)
builder.Text(
    x: 50,
    y: 50,
    text: "PRIORITY SHIPMENT",
    font: ZplFont.Font0,
    height: 40,
    width: 40,
    orientation: ZplOrientation.Normal);

// Using rotated orientation (Normal, Rotated90, Inverted180, BottomUp270)
builder.Text(50, 120, "ROTATED", ZplFont.FontA, 30, 30, ZplOrientation.Rotated90);
```

---

## 3. Barcodes and 2D QR Codes

### 3.1. Code 128 (`^BC`)
```csharp
builder.BarcodeCode128(
    x: 50,
    y: 180,
    data: "TRACK-889900",
    height: 70,
    showText: true,
    orientation: ZplOrientation.Normal);
```

### 3.2. Code 39 (`^B3`)
```csharp
builder.BarcodeCode39(
    x: 50,
    y: 280,
    data: "PART-1234",
    height: 60,
    showText: true,
    orientation: ZplOrientation.Normal);
```

### 3.3. EAN-13 (`^BE`)
```csharp
builder.BarcodeEan13(
    x: 50,
    y: 380,
    data: "4006381333931",
    height: 70,
    showText: true,
    orientation: ZplOrientation.Normal);
```

### 3.4. 2D QR Code (`^BQ`)
```csharp
builder.QrCode(
    x: 450,
    y: 180,
    data: "https://ericksonlopez.dev/p/99",
    magnification: 5,
    orientation: ZplOrientation.Normal);
```

---

## 4. Geometric Primitives

```csharp
// Rectangular box or border (^GB)
builder.Box(x: 20, y: 20, width: 760, height: 1160, borderThickness: 3);

// Circle (^GC)
builder.Circle(x: 200, y: 500, diameter: 80, borderThickness: 2);

// Ellipse (^GE)
builder.Ellipse(x: 350, y: 500, width: 120, height: 60, borderThickness: 2);

// Diagonal lines (^GD)
builder.DiagonalLine(x: 500, y: 500, width: 80, height: 80, borderThickness: 3, rightLeaning: true);  // /
builder.DiagonalLine(x: 600, y: 500, width: 80, height: 80, borderThickness: 3, rightLeaning: false); // \
```

---

## 5. Logos and Images in ZPL (`Zpl.Imaging`)

Install the satellite package:
```bash
dotnet add package EricksonLopez.Printing.Zpl.Imaging
```

```csharp
using EricksonLopez.Printing.Zpl.Imaging;

byte[] logoBmp = File.ReadAllBytes("company_logo.bmp");

// Injects a ^GF graphic field encoded in hexadecimal with Floyd-Steinberg dithering
builder.Image(x: 50, y: 50, logoBmp, ZplDitherAlgorithm.FloydSteinberg);
```

---

> **Navigation:** [⬅️ ESC/POS Reference](escpos-command-reference.md) | [Documentation Index](README.md) | [Next: SignalR Web-to-Edge Guide ➡️](web-to-edge-guide.md)
