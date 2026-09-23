<!-- Copyright © Erickson Lopez. MIT License. -->
# Frequently Asked Questions (FAQ) — EricksonLopez.Printing

Answers to common architectural, operational, and development questions regarding the `EricksonLopez.Printing` library ecosystem.

---

### 1. What exactly is `EricksonLopez.Printing`?
It is a modern, high-performance, zero-allocation .NET (8.0, 9.0, and 10.0) library ecosystem designed to compile and stream raw printer bytecode directly to Point-of-Sale thermal receipt printers (ESC/POS) and industrial label printers (Zebra ZPL II) over raw TCP sockets, RS-232 serial COM ports, and remote SignalR WebSocket bridges.

---

### 2. Does it require installing Windows desktop print spoolers or vendor drivers?
**No.** `EricksonLopez.Printing` communicates directly in native hardware bytecode (JetDirect / RAW socket port 9100 or raw serial COM streams). It does not depend on `winspool.drv`, GDI+, Windows desktop printer drivers, or Linux CUPS. It functions identically on Windows, Linux, Alpine containers, macOS, and edge gateways.

---

### 3. How does it differ from `ESCPOS_NET` or `BinaryKits.Zpl`?
- **Exception-Free Railway Handling**: Uses `EricksonLopez.Result` with strongly-typed error codes (`PrintingErrorCodes`). Hardware drops and network timeouts never throw unhandled exceptions into application business logic.
- **Zero-Allocation Architecture**: Uses `RecyclableMemoryStreamManager`, sliding-window line buffers, and `ArrayPool<byte>` to avoid promoting byte buffers into the Large Object Heap (LOH).
- **Native AOT & Trimming**: Engineered with `<IsAotCompatible>true</IsAotCompatible>` and zero unpreserved dynamic reflection, compiling single-file Native AOT binaries that boot in under 15ms.
- **Distributed Cloud-to-Edge Dispatch**: Includes native ASP.NET Core SignalR hub infrastructure (`PrinterHub`, `HubPrinterDispatcher`) allowing central SaaS servers to dispatch print jobs to branch-level printers behind corporate NAT firewalls.

---

### 4. Can I print international and accented characters (e.g., Spanish tildes, French accents)?
**Yes.** When instantiating `EscPosBuilder`, pass the appropriate regional `Encoding` corresponding to the hardware code page configured on your thermal printer (typically `Windows-1252` or `CP850`):

```csharp
var encoding = Encoding.GetEncoding(1252);
using var builder = new EscPosBuilder(encoding: encoding);
```

---

### 5. How do I trigger the cash drawer pulse?
Invoke `.OpenCashDrawer()` on `EscPosBuilder`, which generates the standard `ESC p 0 25 250` electrical pulse to the cash drawer RJ11/RJ12 solenoid:

```csharp
builder
    .Line("Cash Sale")
    .OpenCashDrawer()
    .Feed(3)
    .Cut();
```

---

### 6. How can I test my printing code without physical hardware?
Several verified options exist:
1. **Official Showcase CLI**: Run the interactive Showcase project demonstrating all 11 progressive levels:
   ```bash
   dotnet run --project samples/EricksonLopez.Printing.Showcase --framework net10.0 -- all
   ```
2. **Raw TCP Socket Simulator (Netcat / Ncat)**:
   ```bash
   # Listen on raw port 9100 and observe transmitted byte stream
   ncat -l -p 9100 --keep-open
   ```
3. **Online ZPL Viewer**: Take the string output of `zplBuilder.Build().GetBytes()` and paste it into [Labelary Online ZPL Viewer](http://labelary.com/viewer.html) to inspect visual rendering.

---

### 7. Why did an automatic retry policy duplicate a physical receipt?
A mid-stream network drop (`Printer.TransmissionError`) can sever the TCP socket after the physical printer has already received bytes and advanced paper.  
Consult the [Best Practices Guide](best-practices.md) for the **Golden Rule of Physical Idempotency** and why `RetryOnTransmissionError` must remain `false` for commercial and fiscal transactions.

---

### 8. Can I print store logos and customer signatures?
**Yes.** The satellite package `EricksonLopez.Printing.EscPos.Imaging` provides `.Image()` and `.RasterImage()` extension methods using pure C# `FloydSteinberg` and `Threshold` dithering algorithms. Remember to pass `safeMode: false` to `EscPosBuilder` when printing binary raster graphics.
