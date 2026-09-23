<!-- Copyright © Erickson Lopez. MIT License. -->
# Public API Inventory — EricksonLopez.Printing

This document establishes the exhaustive inventory and **single source of truth** for the public API surface of the `EricksonLopez.Printing` library ecosystem, covering all public interfaces, classes, structs, records, enums, extension methods, and dependency injection services.

---

## 1. Project Classification & Architectural Roles

| Project | Path | Classification | Architectural Role |
|---|---|---|---|
| `EricksonLopez.Printing` | `src/EricksonLopez.Printing` | **Core Library** | Fundamental printing contracts (`IPrinterClient`, `IPrintDocument`), raw TCP network clients (ephemeral and persistent pooled), resilience decorator with retries, and DI extensions. |
| `EricksonLopez.Printing.EscPos` | `src/EricksonLopez.Printing.EscPos` | **Core Library** | Fluent ESC/POS thermal receipt bytecode builder (`EscPosBuilder`), typography styles, table formatting, 1D barcodes, and QR codes. |
| `EricksonLopez.Printing.Zpl` | `src/EricksonLopez.Printing.Zpl` | **Core Library** | Fluent Zebra ZPL II industrial label generator (`ZplBuilder`), strongly-typed fonts, barcodes, and geometric primitives. |
| `EricksonLopez.Printing.EscPos.Imaging` | `src/EricksonLopez.Printing.EscPos.Imaging` | **Infrastructure** | 1-bit monochrome raster conversion pipeline for ESC/POS with Threshold and Floyd-Steinberg dithering (`MonochromeBitmapConverter`). |
| `EricksonLopez.Printing.EscPos.Status` | `src/EricksonLopez.Printing.EscPos.Status` | **Infrastructure** | Real-time `DLE EOT` hardware telemetry queries and sensor response decoding (`EscPosStatusCommands`, `EscPosStatusParser`). |
| `EricksonLopez.Printing.Serial` | `src/EricksonLopez.Printing.Serial` | **Infrastructure** | RS-232 / Virtual COM port serial driver with channel mutual exclusion enforced via `SemaphoreSlim`. |
| `EricksonLopez.Printing.SignalR` | `src/EricksonLopez.Printing.SignalR` | **Infrastructure** | Remote cloud-to-edge hardware dispatch infrastructure via ASP.NET Core SignalR (`PrinterHub`, `HubPrinterDispatcher`). |
| `EricksonLopez.Printing.Zpl.Imaging` | `src/EricksonLopez.Printing.Zpl.Imaging` | **Infrastructure** | Monochrome rasterization and `^GF` (Graphic Field) hexadecimal command generator for Zebra printers. |
| `EricksonLopez.Printing.Showcase` | `samples/EricksonLopez.Printing.Showcase` | **Samples** | Official reference implementation and executable documentation CLI. |
| `EricksonLopez.Printing.Tests` | `tests/EricksonLopez.Printing.Tests` | **Tests** | Unit tests, integration tests, concurrency tests, and regression suites. |
| `EricksonLopez.Printing.SignalR.Tests` | `tests/EricksonLopez.Printing.SignalR.Tests` | **Tests** | Unit tests for SignalR Hub and Dispatcher components. |

---

## 2. Detailed Public API Catalog

### 2.1. EricksonLopez.Printing (Core Library)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `IPrinterClient` | `EricksonLopez.Printing` | Primary contract for transmitting print documents to network, serial, or cloud targets. | `EricksonLopez.Result` | Fundamental | Level 01, 02, 05, 06, 08 |
| `IPrintDocument` | `EricksonLopez.Printing` | Document payload contract containing byte streams, idempotency metadata, and memory representations. | `System.IO`, `System.Threading` | Fundamental | Level 01, 07, 08 |
| `RawPrintDocument` | `EricksonLopez.Printing` | In-memory implementation of `IPrintDocument` backed by a byte array with idempotency tracking. | `IPrintDocument` | Fundamental | Level 01, 05, 06, 08, 09 |
| `TcpPrinterClientOptions` | `EricksonLopez.Printing` | Raw TCP socket connection parameters (`Host`, `Port`, `TimeoutMs`, `NoDelay`, `LingerSeconds`). | None | Intermediate | Level 02, 05 |
| `TcpPrinterClient` | `EricksonLopez.Printing` | Ephemeral TCP socket client creating and tearing down network connections per print job. | `TcpPrinterClientOptions`, `ILogger` | Intermediate | Level 02, 05 |
| `PooledTcpPrinterClient` | `EricksonLopez.Printing` | High-throughput persistent TCP client maintaining open sockets and serializing via `SemaphoreSlim`. | `TcpPrinterClientOptions`, `ILogger` | Advanced | Level 02, 05 |
| `ResilientPrinterOptions` | `EricksonLopez.Printing` | Exponential backoff options (`MaxRetries`, `InitialDelay`, `MaxDelay`, `RetryOnTransmissionError`). | None | Intermediate | Level 06 |
| `ResilientPrinterClient` | `EricksonLopez.Printing` | Resilient decorator over `IPrinterClient` with backoff and physical idempotency protection. | `IPrinterClient`, `TimeProvider` | Advanced | Level 06 |
| `ResilientPrintingExtensions` | `EricksonLopez.Printing` | Fluent `.WithRetry()` extension method to wrap any `IPrinterClient`. | `IPrinterClient`, options | Fundamental | Level 06, 08 |
| `PrintingServiceCollectionExtensions` | `EricksonLopez.Printing` | Microsoft DI registration extensions (`AddTcpPrinter`, `AddKeyedTcpPrinter`, `AddPooledTcpPrinter`, `AddKeyedPooledTcpPrinter`). | `IServiceCollection` | Intermediate | Level 02 |
| `PrintingErrorCodes` | `EricksonLopez.Printing` | Standardized error code constants (`SocketError`, `TransmissionError`, `Timeout`, `Canceled`). | None | Fundamental | Level 05, 06 |
| `PrintingEventIds` | `EricksonLopez.Printing` | Standardized `EventId` constants (1001–1011) for structured logging and OpenTelemetry. | `Microsoft.Extensions.Logging` | Intermediate | Level 10 |

---

### 2.2. EricksonLopez.Printing.EscPos (Core Library)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `EscPosAlignment` | `EricksonLopez.Printing.EscPos` | Text and element justification enum (`Left = 0`, `Center = 1`, `Right = 2`). | None | Fundamental | Level 01, 03, 04, 07 |
| `EscPosQrErrorCorrection` | `EricksonLopez.Printing.EscPos` | Error correction enum for Model 2 QR codes (`L`, `M`, `Q`, `H`). | None | Intermediate | Level 03 |
| `EscPosUnderline` | `EricksonLopez.Printing.EscPos` | Underline font styling enum (`None = 0`, `SingleDot = 1`, `DoubleDot = 2`). | None | Fundamental | Level 03 |
| `EscPosBuilder` | `EricksonLopez.Printing.EscPos` | Fluent, single-threaded ESC/POS compiler using recyclable memory streams and ArrayPool buffers. | `RecyclableMemoryStreamManager` | Intermediate | Level 01, 03, 04, 07 |

---

### 2.3. EricksonLopez.Printing.EscPos.Imaging (Infrastructure)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `EscPosDitherAlgorithm` | `EricksonLopez.Printing.EscPos.Imaging` | Monochrome dithering algorithm enum (`Threshold`, `FloydSteinberg`). | None | Intermediate | Level 04 |
| `EscPosImageBuilderExtensions` | `EricksonLopez.Printing.EscPos.Imaging` | Fluent `.Image()` extension methods for `EscPosBuilder`. | `EscPosBuilder`, converter | Intermediate | Level 04 |
| `MonochromeBitmapConverter` | `EricksonLopez.Printing.EscPos.Imaging` | Pure C# converter transforming BMP or RGB buffers to packed 1-bit ESC/POS raster images (`GS v 0`). | `ArrayPool<byte>`, `ArrayPool<int>` | Advanced | Level 04 |

---

### 2.4. EricksonLopez.Printing.EscPos.Status (Infrastructure)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `EscPosPrinterStatus` | `EricksonLopez.Printing.EscPos.Status` | Immutable record containing hardware sensor status flags (`IsOnline`, `IsCoverOpen`, `IsPaperOut`, `IsPaperNearEnd`, `IsDrawerOpen`, `HasError`, `HasCutterError`). | None | Intermediate | Level 10 |
| `EscPosStatusCommands` | `EricksonLopez.Printing.EscPos.Status` | Pre-allocated `ReadOnlyMemory<byte>` command sequences for real-time `DLE EOT 1..4` polling queries. | `ReadOnlyMemory<byte>` | Intermediate | Level 10 |
| `EscPosStatusParser` | `EricksonLopez.Printing.EscPos.Status` | Zero-allocation byte parser decoding individual status bytes and assembling unified `EscPosPrinterStatus`. | `EscPosPrinterStatus` | Advanced | Level 10 |

---

### 2.5. EricksonLopez.Printing.Serial (Infrastructure)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `SerialPrinterClientOptions` | `EricksonLopez.Printing` | Serial COM port options (`PortName`, `BaudRate`, `Parity`, `DataBits`, `StopBits`, `Handshake`, `Timeouts`). | `System.IO.Ports` | Intermediate | Level 02 |
| `SerialPrinterClient` | `EricksonLopez.Printing.Serial` | RS-232 / Virtual COM port driver ensuring mutual exclusion via `SemaphoreSlim(1, 1)`. | `SerialPort`, `IPrinterClient` | Advanced | Level 02 |
| `PrintingSerialServiceCollectionExtensions` | `EricksonLopez.Printing` | DI extensions `AddSerialPrinter` and `AddKeyedSerialPrinter`. | `IServiceCollection` | Intermediate | Level 02 |

---

### 2.6. EricksonLopez.Printing.SignalR (Infrastructure)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `PrintJobMessage` | `EricksonLopez.Printing.SignalR` | Transport DTO record carrying Base64 print job payload, printer name, and timestamp. | `DateTimeOffset` | Fundamental | Level 09 |
| `IPrinterHubClient` | `EricksonLopez.Printing.SignalR` | Strongly-typed client interface for edge daemons listening to `OnPrintJobReceived`. | `PrintJobMessage` | Intermediate | Level 09 |
| `PrinterHub` | `EricksonLopez.Printing.SignalR` | ASP.NET Core SignalR hub managing printer registrations and group routing (`printer:NAME`). | `Hub<IPrinterHubClient>` | Advanced | Level 09 |
| `IHubPrinterDispatcher` | `EricksonLopez.Printing.SignalR` | High-level dispatch abstraction consumed by backend microservices to send jobs to edge printers. | `IPrintDocument`, `Result` | Intermediate | Level 02, 09 |
| `HubPrinterDispatcher` | `EricksonLopez.Printing.SignalR` | Implementation of `IHubPrinterDispatcher` encoding documents and emitting to SignalR groups with telemetry. | `IHubContext`, `TimeProvider`, `ILogger` | Advanced | Level 02, 09 |
| `PrintingSignalRServiceCollectionExtensions` | `EricksonLopez.Printing.SignalR` | DI registration extension `AddPrintingSignalR()`. | `IServiceCollection` | Intermediate | Level 02 |
| `SignalRPrintingEventIds` | `EricksonLopez.Printing.SignalR` | Standardized `EventId` constants (2001–2003) for cloud dispatch telemetry. | `Microsoft.Extensions.Logging` | Intermediate | Level 09 |

---

### 2.7. EricksonLopez.Printing.Zpl (Core Library)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `ZplFont` | `EricksonLopez.Printing.Zpl` | Strongly-typed enum of native Zebra fonts (`Font0`, `FontA`..'FontH'). | None | Fundamental | Level 01, 03, 04, 07 |
| `ZplOrientation` | `EricksonLopez.Printing.Zpl` | Rotation enum for ZPL elements (`Normal`, `Rotated90`, `Inverted180`, `BottomUp270`). | None | Fundamental | Level 03 |
| `ZplPrintDocument` | `EricksonLopez.Printing.Zpl` | Optimized `IPrintDocument` streaming ZPL strings directly to streams via `StreamWriter`. | `IPrintDocument`, `StreamWriter` | Intermediate | Level 01, 03, 04, 07 |
| `ZplBuilder` | `EricksonLopez.Printing.Zpl` | Fluent Zebra ZPL II generator with command injection sanitization and geometric primitives. | `StringBuilder`, `ZplFont` | Intermediate | Level 01, 03, 04, 07 |

---

### 2.8. EricksonLopez.Printing.Zpl.Imaging (Infrastructure)

| Symbol | Namespace | Responsibility | Dependencies | Complexity | Showcase Reference |
|---|---|---|---|---|---|
| `ZplDitherAlgorithm` | `EricksonLopez.Printing.Zpl.Imaging` | Monochrome dithering algorithm enum for Zebra labels (`Threshold`, `FloydSteinberg`). | None | Intermediate | Level 04 |
| `ZplGraphicFieldConverter` | `EricksonLopez.Printing.Zpl.Imaging` | Converter formatting monochrome raster data into hexadecimal ZPL II `^GF` commands. | `Convert.ToHexString` | Advanced | Level 04 |
| `MonochromeBitmapConverter` | `EricksonLopez.Printing.Zpl.Imaging` | Pure C# converter transforming BMP or RGB buffers into 1-bit packed raster for ZPL labels. | `ArrayPool<byte>`, `ArrayPool<int>` | Advanced | Level 04 |
| `ZplImageBuilderExtensions` | `EricksonLopez.Printing.Zpl.Imaging` | Fluent `.Image()` and `.GraphicField()` extension methods for `ZplBuilder`. | `ZplBuilder`, converter | Intermediate | Level 04 |

---

## 3. Showcase Verification Metrics

- **Total Public Types Cataloged**: 40
- **Total Types Covered in Showcase**: 40 (100% Coverage)
- **Fictitious or Obsolete APIs in Showcase**: 0
- **Compilation Warnings**: 0 (`TreatWarningsAsErrors=true`)
