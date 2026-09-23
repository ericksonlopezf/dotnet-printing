<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0012: Rejection of Local ZPL Label Rendering Engine

> **Navigation:** [⬅️ ADR-0011 (Resilience Decorator)](adr-0011-transient-resilience-and-retry-decorator.md) | [ADR Index](README.md) | [Next: ADR-0013 (Rejection of Bluetooth) ➡️](adr-0013-rejection-of-bluetooth-transport.md)

---

## Status
**Rejected** (2026-09-03)

## Context
Some competitor libraries (such as `BinaryKits.Zpl.Viewer`) implement a local rasterization engine that parses ZPL commands (boxes, barcodes, fonts) and paints them onto an in-memory canvas using SkiaSharp or System.Drawing to produce a PNG/JPEG preview during development.

We evaluated building a similar in-process ZPL viewer/rasterizer within `EricksonLopez.Printing.Zpl`.

## Decision
Implementing a local ZPL rasterization and preview engine is **formally rejected**.

## Rationale
1. **Disproportionate Complexity & Fidelity:** Faithfully replicating Zebra proprietary firmware rasterization (bitmap font scaling, 2D barcode ratios, printhead offsets) requires thousands of lines of code and ongoing maintenance that will never match physical hardware exactly.
2. **Binary Footprint & AOT Impact:** Mandates heavy unmanaged dependencies (SkiaSharp) that compromise Native AOT profiles and inflate binary sizes for a feature used only in development, never in production.
3. **Superior De Facto Standards:** Cloud rendering services like [Labelary](http://labelary.com) provide free, pixel-perfect ZPL preview generation.

## Related Decisions
- **Derived from:** [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Native AOT invariant forbids heavy unmanaged graphics libraries in production runtime).
- **Complements:** [ADR-0009](adr-0009-decoupled-satellite-imaging-packages.md) (Image processing isolated strictly to satellite packages).

## Recommended Alternative
For developers needing ZPL label preview during development:
1. Use the free [Labelary Online Viewer](http://labelary.com/viewer.html).
2. Or invoke the public Labelary REST API with the string from `ZplBuilder.Build()` via a standard `HttpClient` in integration tests or internal designer tools.

---

> **Navigation:** [⬅️ ADR-0011 (Resilience Decorator)](adr-0011-transient-resilience-and-retry-decorator.md) | [ADR Index](README.md) | [Next: ADR-0013 (Rejection of Bluetooth) ➡️](adr-0013-rejection-of-bluetooth-transport.md)
