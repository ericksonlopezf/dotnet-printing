# ESC/POS Command Reference — EricksonLopez.Printing.EscPos

> **Navigation:** [Documentation Index](README.md) | [Next: ZPL II Reference ➡️](zpl-command-reference.md)

---

This guide details all commands and features supported by `EscPosBuilder`, `EscPos.Imaging`, and `EscPos.Status`.

---

## 1. Initialization and Core Formatting

| C# Method | ESC/POS Command | Description |
|---|---|---|
| `Initialize()` | `ESC @` (`0x1B, 0x40`) | Resets internal buffer and restores factory default settings |
| `Raw(ReadOnlySpan<byte>)` | Arbitrary | Injects custom raw byte sequences directly into the output stream |
| `Align(EscPosAlignment)` | `ESC a n` (`0x1B, 0x61, n`) | Alignment: `Left` (0), `Center` (1), `Right` (2) |
| `Bold(bool enable)` | `ESC E n` (`0x1B, 0x45, n`) | Enables (`1`) or disables (`0`) emphasized bold mode |
| `Underline(EscPosUnderline)` | `ESC - n` (`0x1B, 0x2D, n`) | Underline mode: `None` (0), `SingleDot` (1), `DoubleDot` (2) |
| `DoubleSize(bool w, bool h)` | `GS ! n` (`0x1D, 0x21, n`) | Toggles double width (bit 5) and/or double height (bit 0) |
| `FontSize(int w, int h)` | `GS ! n` (`0x1D, 0x21, n`) | Scalable font sizing multiplier (1x to 8x width and height) |
| `Feed(int lines)` | `ESC d n` (`0x1B, 0x64, n`) | Feeds paper forward `n` print lines (1 to 255) |
| `Cut(bool partial)` | `GS V m` (`0x1D, 0x56, m`) | Paper cut: full cut (`0x00`) or partial cut with tab (`0x01`) |
| `OpenCashDrawer()` | `ESC p 0 25 250` | Sends electrical pulse to RJ12 pin to kick the cash drawer |

---

## 2. Typography and Tabular Layout

```csharp
using var builder = new EscPosBuilder();
builder
    .Initialize()
    .Align(EscPosAlignment.Center)
    .FontSize(widthMultiplier: 2, heightMultiplier: 2)
    .Line("STORE TITLE")
    .FontSize(1, 1)
    .Underline(EscPosUnderline.SingleDot)
    .Line("Underlined Subtitle")
    .Underline(EscPosUnderline.None)
    .Divider(width: 42, character: '=')
    .TableRow("American Coffee", "$3.50", totalWidth: 42, fillChar: '.')
    .TableRow("Special Sandwich", "$8.00", totalWidth: 42, fillChar: '.')
    .Divider();
```

---

## 3. Barcodes and 2D QR Codes

### 3.1. Code 128
```csharp
builder.BarcodeCode128("INV-987654", height: 64, showHri: true);
```
- Validates data payload length (maximum 255 bytes).
- Emits `GS H 2` (HRI below), `GS h n` (height), and `GS k 73 len bytes...`.

### 3.2. Code 39
```csharp
builder.BarcodeCode39("CODE39-TEST", height: 60, width: 3, showHri: true);
```
- Supports characters A–Z, 0–9, space, and `-$%./+`.
- Emits `GS w n` (module width), `GS H n`, `GS h n`, and `GS k 69 len bytes...`.

### 3.3. EAN-13
```csharp
// Providing 12 digits: the checksum digit is calculated automatically
builder.BarcodeEan13("400638133393", height: 64, width: 2, showHri: true);

// Providing 13 full digits: the checksum digit is mathematically validated
builder.BarcodeEan13("4006381333931");
```

### 3.4. 2D QR Code
```csharp
builder.QrCode("https://ericksonlopez.dev", moduleSize: 6, errorCorrection: EscPosQrErrorCorrection.M);
```
- Generates standard Epson Model 2 QR code command sequences with error correction levels L, M, Q, H.

---

## 4. Images and Logos (`EscPos.Imaging`)

To print corporate logos and bitmap graphics, install the satellite package:
```bash
dotnet add package EricksonLopez.Printing.EscPos.Imaging
```

### 4.1. Load BMP File
```csharp
using EricksonLopez.Printing.EscPos.Imaging;

byte[] bmpBytes = File.ReadAllBytes("logo.bmp");
builder.Image(bmpBytes, EscPosDitherAlgorithm.FloydSteinberg);
```

### 4.2. In-Memory Raw Pixel Buffer
```csharp
builder.Image(
    width: 200,
    height: 100,
    pixelData: rawRgbBytes,
    bytesPerPixel: 3,
    algorithm: EscPosDitherAlgorithm.Threshold);
```

---

## 5. Hardware Telemetry and Sensors (`EscPos.Status`)

Install the telemetry satellite package:
```bash
dotnet add package EricksonLopez.Printing.EscPos.Status
```

```csharp
using EricksonLopez.Printing.EscPos.Status;

// Query real-time paper sensor status (DLE EOT 4)
await serialPort.BaseStream.WriteAsync(EscPosStatusCommands.QueryPaperSensor);
var responseByte = (byte)serialPort.BaseStream.ReadByte();

var (paperNearEnd, paperOut) = EscPosStatusParser.ParsePaperSensorByte(responseByte);
if (paperOut)
{
    Console.WriteLine("ALERT: Printer is out of paper.");
}
```

---

> **Navigation:** [Documentation Index](README.md) | [Next: ZPL II Reference ➡️](zpl-command-reference.md)
