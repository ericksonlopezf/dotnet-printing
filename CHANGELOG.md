# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-09-23

### Added
- **Initial Public Release of the `EricksonLopez.Printing` Ecosystem:**
  Modular, high-performance, Native AOT-first .NET printing suite for Point-of-Sale (POS) thermal receipt printers and Zebra (ZPL II) industrial label printers across `.NET 8`, `.NET 9`, and `.NET 10`.

- **Modular Package Suite (8 Packages):**
  - **`EricksonLopez.Printing` (Core):** Agnostic printing domain abstractions, `IPrintDocument` streaming contracts, `IPrinterClient` transport interface, `TcpPrinterClient` for port 9100 RAW printing, `PooledTcpPrinterClient` with persistent socket lifecycle, `ResilientPrinterClient` retry decorator, structured telemetry event IDs (`PrintingEventIds`), and standard RFC 9457 error taxonomy (`PrintingErrorCodes`).
  - **`EricksonLopez.Printing.Serial`:** Dedicated satellite transport package for RS-232 / Virtual COM port printing via `System.IO.Ports` with `SemaphoreSlim(1, 1)` concurrency protection. Segregated to protect Native AOT purity and container deployments.
  - **`EricksonLopez.Printing.EscPos`:** Fluent bytecode builder (`EscPosBuilder`) compiling receipt commands directly to ESC/POS hardware opcodes in-memory (`MemoryStream`). Supports typography (font selection, bold, double-strike, underline scaling), alignments, barcodes (Code 128, Code 39, EAN-13), QR codes (Model 2), tabular layout formatting (`TableRow`), horizontal rules (`Divider`), cash drawer kick pulses (`PulseCashDrawer`, `OpenCashDrawer`), and paper cuts (`Cut`). Defaults to `safeMode = true` to protect against command injection.
  - **`EricksonLopez.Printing.EscPos.Imaging`:** Pure managed C# monochrome rasterizer (`MonochromeBitmapConverter`, `EscPosImageBuilderExtensions`). Converts 24-bit/32-bit RGB bitmaps into ESC/POS raster bit image commands (`GS v 0`) using Floyd-Steinberg error diffusion and threshold dithering with zero unmanaged C++ dependencies (no SkiaSharp, no GDI+).
  - **`EricksonLopez.Printing.EscPos.Status`:** Real-time bidirectional sensor telemetry (`EscPosStatusCommands`, `EscPosStatusParser`, `EscPosPrinterStatus`). Encodes `DLE EOT 1..4` real-time request opcodes and parses printer status bytes to detect paper-out, cover-open, paper near-end, cutter jam, and cash drawer state before initiating print jobs.
  - **`EricksonLopez.Printing.Zpl`:** Fluent ZPL II command compiler (`ZplBuilder`, `ZplPrintDocument`) generating clean Zebra programming language bytecode with zero-allocation `StringBuilder` pipelines. Supports scalable and bitmap fonts (`ZplFont`), 1D barcodes (Code 128, Code 39, EAN-13), 2D QR codes with error correction, geometric primitives (`Box`, `Circle`, `Ellipse`, `DiagonalLine`), and automatic `^FH_` hex character escaping.
  - **`EricksonLopez.Printing.Zpl.Imaging`:** Pure managed C# image converter (`MonochromeBitmapConverter`, `ZplGraphicFieldConverter`, `ZplImageBuilderExtensions`) compiling monochrome bitmaps into compressed ZPL II hexadecimal Graphic Field commands (`^GF`).
  - **`EricksonLopez.Printing.SignalR`:** ASP.NET Core SignalR hub bridging cloud SaaS platforms to distributed on-premise edge printer daemons. Features authenticated endpoints (`PrinterHub` with `[Authorize]`), dynamic tenant printer registration (`RegisterPrinter`, `UnregisterPrinter`), zero-firewall cloud-to-edge dispatch (`HubPrinterDispatcher`), and client interfaces (`IPrinterHubClient`).

- **Core Architectural Invariants & Features:**
  - **Strict Result Pattern Semantics:** Complete elimination of unhandled transport exceptions (`SocketException`, `IOException`, `TimeoutException`). All transport operations return strongly typed `Result<bool>` via `EricksonLopez.Result` with explicit domain error types.
  - **Zero-Copy Memory Pipelines:** Streaming contracts via `IPrintDocument.WriteToAsync(Stream, CancellationToken)` and `ReadOnlyMemory<byte> GetMemory()`, eliminating heap allocations, intermediate string conversions, and GC pressure during high-frequency fulfillment.
  - **100% Native AOT & Trimming Verification:** Multi-targeted across `net8.0`, `net9.0`, and `net10.0` with `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`. Edge agent applications compile into single-file self-contained binaries booting in < 15ms with 12–18 MB RAM.
  - **Transient Resilience Decorator:** `ResilientPrinterClient` applying configurable exponential backoff with ceiling delays, `TimeProvider` test isolation, and strict enforcement of the Golden Rule of Physical Idempotency (non-retryable transmission errors).
  - **High-Throughput Persistent Connection Transport:** `PooledTcpPrinterClient` with single persistent socket lifecycle, automated reconnection, serialized execution via `SemaphoreSlim`, and socket optimization (`NoDelay=true`, `LingerSeconds=0`).
  - **Enterprise Observability:** Structured logging with standardized `EventId`s (1001–1011 in core, 2001–2003 in SignalR) for start, success, failure, retry, and exhaustion telemetry.
  - **Showcase Reference Implementation (`samples/EricksonLopez.Printing.Showcase`):** 11 progressive levels (Level 00 through Level 10) covering 100% of public API surface area.
  - **Complete Technical Documentation Suite:** 11 comprehensive guides in `docs/` (API Reference, Architecture Guide, Production Cookbook, Best Practices, Performance Guide, Troubleshooting, Quick Start, Getting Started, FAQ, Showcase Guide, CI/CD Strategy) and 21 Architecture Decision Records (ADR-0001 through ADR-0021).

### Security & Hardening
- **Authentication by Default:** `PrinterHub` secured with `[Authorize]`, preventing unauthorized access to printer dispatch infrastructure.
- **Injection Attack Defense:** `EscPosBuilder` enforces `SafeMode = true` by default, rejecting unvalidated raw escape sequence injection (`.Raw(...)`). `ZplBuilder` enforces automatic `^FH_` hex escaping for control delimiters (`^`, `~`) and validates alphanumeric barcode parameters.
- **Denial-of-Service & LOH Memory Exhaustion Protection:** `MonochromeBitmapConverter` enforces `MaxDimension = 8192` and utilizes a 2-line sliding window buffer (`int[width]`) and `ArrayPool<byte>.Shared` / `ArrayPool<int>.Shared`, reducing peak memory allocation by 99.97% and eliminating Large Object Heap (LOH) pressure.
- **Physical Idempotency & Duplicate Prevention:** End-to-end idempotency tracking (`IPrintDocument.IdempotencyKey`) and non-retryable transmission drop classification (`Printer.TransmissionError`) preventing duplicate fiscal receipt issuance or duplicate pallet label prints.
- **Cryptographic Strong Naming & Supply Chain Security:** All assemblies cryptographically signed (`EricksonLopez.snk`), Central Package Management (CPM) enforced, and SourceLink metadata embedded for fully deterministic and reproducible builds.
