<!-- Copyright © Erickson Lopez. MIT License. -->
# Troubleshooting & Diagnostics Guide — EricksonLopez.Printing

This guide details common operational anomalies, hardware state conflicts, and connectivity failures when transmitting print jobs to thermal receipt and industrial label printers, along with verified diagnostic steps and solutions.

---

## 1. Standard Error Taxonomy (`PrintingErrorCodes`)

The library uses `EricksonLopez.Result` to return typed domain errors rather than throwing unhandled exceptions:

| Error Code | Probable Root Cause | Corrective Action |
|---|---|---|
| `Printer.SocketError` | Unable to connect to printer IP on port 9100. | Check power, Ethernet cabling, DHCP/static IP assignment, subnet routing, and firewall rules. |
| `Printer.Timeout` | Printer did not respond within `TimeoutMs`. | Check if printer is sleeping, paper roll is jammed, or firmware receive buffer is locked by a frozen job. |
| `Printer.TransmissionError` | Connection dropped abruptly *during* active byte transmission. | **CAUTION**: Do not retry blindly. Check whether physical paper already advanced and cut to avoid duplicate charging/ticketing. |
| `Printer.Canceled` | Operation was aborted by the provided `CancellationToken`. | Check if client HTTP request timed out or background worker service triggered cancellation. |

> **Note on Exceptions & Empty Payloads:**
> - **Disposed Clients**: Attempting to transmit via a disposed client instance throws `ObjectDisposedException` immediately (register long-lived clients as Singletons in DI).
> - **Empty Payloads**: If an `IPrintDocument` has `IsEmpty == true`, the client treats it as an idempotent no-op and returns `Result<bool>.Success(true)` without opening network sockets.

---

## 2. Common Issues & Solutions

### 2.1. "Corrupted text or strange hieroglyphic characters printed"
- **Root Cause**: Discrepancy between the printer hardware firmware code page and the character encoding used by C#.
- **Solution**: Pass the explicit regional `Encoding` to `EscPosBuilder`:
  ```csharp
  // For Western European accents, Spanish tildes, and Latin characters (CP850 / Windows-1252):
  var encoding = Encoding.GetEncoding(1252);
  using var builder = new EscPosBuilder(encoding: encoding);
  ```

### 2.2. "The printer prints the first ticket then rejects all subsequent connections"
- **Root Cause**: Many low-cost thermal POS printers support only **a single active TCP connection**. If using `TcpPrinterClient` under high concurrency, lingering sockets in `TIME_WAIT` lock port 9100.
- **Solution**: Switch to `PooledTcpPrinterClient`. It keeps a single socket continuously open, queues concurrent requests through an internal `SemaphoreSlim`, and prevents socket exhaustion:
  ```csharp
  services.AddPooledTcpPrinter(opt =>
  {
      opt.Host = "192.168.1.200";
      opt.Port = 9100;
      opt.NoDelay = true;
  });
  ```

### 2.3. "System.UnauthorizedAccessException on RS-232 / COM Ports"
- **Root Cause**: The physical serial port is held exclusively by another process (OS spooler driver, electronic scale daemon, or payment terminal service).
- **Solution**:
  - Close any external software locking the target COM port.
  - Ensure `SerialPrinterClient` is registered as a **Singleton** in DI, preventing multiple instances from opening the same physical port concurrently.

### 2.4. "Receipt paper does not feed past the cutter blade"
- **Root Cause**: Thermal printer cutter blades are physically situated 2 to 3 cm above the thermal heating element. Cutting immediately after text truncates the final line.
- **Solution**: Add `.Feed(3)` before calling `.Cut()`:
  ```csharp
  builder
      .Line("Thank you for your visit!")
      .Feed(3) // Advances paper past the cutter blade
      .Cut();  // Full cut (use Cut(partial: true) for partial tab)
  ```

### 2.5. "Diagnosing physical hardware state before initiating printing"
If the printer ignores commands or an indicator LED flashes amber/red:
- Query real-time status opcodes using `EscPosStatusCommands` and decode with `EscPosStatusParser`:
  ```csharp
  // Read 4 status bytes from network stream
  EscPosPrinterStatus status = EscPosStatusParser.Parse(b1, b2, b3, b4);
  
  if (status.IsPaperOut) Console.WriteLine("ALERT: Thermal paper roll is empty!");
  if (status.IsCoverOpen) Console.WriteLine("ALERT: Printer cover is open!");
  if (status.HasCutterError) Console.WriteLine("ALERT: Mechanical cutter blade is jammed!");
  if (status.IsDrawerOpen) Console.WriteLine("INFO: Cash drawer is currently open.");
  ```
