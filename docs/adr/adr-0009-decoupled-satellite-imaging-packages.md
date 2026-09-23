<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0009: Graphic Processing Isolation in Satellite Packages

> **Navigation:** [⬅️ ADR-0008 (DI Lifecycles & Serial Concurrency)](adr-0008-di-client-lifecycles-and-serial-concurrency.md) | [ADR Index](README.md) | [Next: ADR-0010 (Structured Observability) ➡️](adr-0010-structured-logging-and-observability.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Printing commercial logos on ESC/POS thermal receipts and Zebra ZPL labels is a primary requirement in POS and retail logistics systems. However, general-purpose image manipulation libraries (such as `SkiaSharp`, `SixLabors.ImageSharp`, or `System.Drawing.Common`):
1. Introduce bulky native binaries (e.g., `libSkiaSharp.so` / `libSkiaSharp.dylib` exceed 10–20 MB per architecture).
2. Require dynamic OS libraries (e.g., `glibc`, `fontconfig`) frequently missing in distroless or Alpine Linux containers.
3. Introduce warnings and trim-safety hazards under strict Native AOT compilation.

Embedding these dependencies into the core `EricksonLopez.Printing` package would penalize all developers who only print plain receipts, barcodes, or text documents.

## Decision
1. Isolate image conversion into two dedicated satellite packages:
   - `EricksonLopez.Printing.EscPos.Imaging`
   - `EricksonLopez.Printing.Zpl.Imaging`
2. Implement image processing using a standalone, pure C# monochrome engine with zero native dependencies:
   - Luminance threshold dithering.
   - Floyd-Steinberg error diffusion dithering.
   - Native parser for standard 24-bit and 32-bit Windows Bitmaps (BMP) and raw RGB/RGBA pixel buffers.
3. Guarantee that both satellite packages are **100% Native AOT compatible** across Windows, Linux, and macOS.

## Related Decisions
- **Derived from:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Satellite package architecture) and [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Native AOT without native dependencies).
- **Constrains:** [ADR-0012](adr-0012-rejection-of-local-zpl-rendering-engine.md) (Rejection of local ZPL renderer) and [ADR-0016](adr-0016-rejection-of-windows-gdi-system-drawing.md) (Rejection of System.Drawing/GDI).

## Consequences
- Core library footprint remains in kilobytes with zero native dependencies.
- Users requiring logos install satellite packages while preserving Native AOT guarantees without custom Docker native libraries.

---

> **Navigation:** [⬅️ ADR-0008 (DI Lifecycles & Serial Concurrency)](adr-0008-di-client-lifecycles-and-serial-concurrency.md) | [ADR Index](README.md) | [Next: ADR-0010 (Structured Observability) ➡️](adr-0010-structured-logging-and-observability.md)
