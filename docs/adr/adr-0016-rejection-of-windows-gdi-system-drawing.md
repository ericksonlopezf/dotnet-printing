<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0016: Rejection of Printing via Windows GDI or System.Drawing

> **Navigation:** [⬅️ ADR-0015 (Rejection of ZPL Template Engine)](adr-0015-rejection-of-built-in-zpl-template-engine.md) | [ADR Index](README.md) | [Next: ADR-0017 (Rejection of mDNS Discovery) ➡️](adr-0017-rejection-of-mdns-printer-discovery.md)

---

## Status
**Rejected** (2026-09-03)

## Context
The standard Microsoft Windows printing subsystem relies on the Graphical Device Interface (GDI) and the OS print spooler (`winspool.drv` / `PrintDocument` in `System.Drawing.Printing`). Applications draw vector graphics onto a `Graphics` surface, and manufacturer drivers rasterize that content for physical printers.

We evaluated adding a `WindowsGdiPrinterClient` to target printers installed in Windows Control Panel.

## Decision
Integration with Windows GDI and `System.Drawing.Printing` is **categorically rejected**.

## Rationale
1. **Contradicts Core Principles:** `EricksonLopez.Printing` is built explicitly for **direct bytecode, driverless printing**. Using GDI requires manufacturer drivers, Windows spooler queues, and OS spooler delays, forfeiting precise byte-level hardware control.
2. **Cross-Platform Incompatibility & Anti-AOT:** Microsoft deprecated `System.Drawing.Common` on non-Windows platforms in .NET 6+. Its internal P/Invoke calls to `gdi32.dll` break Linux containers, edge environments, and strict Native AOT compilation.
3. **Severe Performance Penalty:** The Windows print spooler introduces noticeable latency (500ms to multiple seconds per job), whereas direct TCP socket or Serial writing completes in under 10 milliseconds.

## Related Decisions
- **Derived from:** [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Cross-platform Native AOT without Windows dependencies) and [ADR-0009](adr-0009-decoupled-satellite-imaging-packages.md) (Autonomous pure C# graphics engine).

---

> **Navigation:** [⬅️ ADR-0015 (Rejection of ZPL Template Engine)](adr-0015-rejection-of-built-in-zpl-template-engine.md) | [ADR Index](README.md) | [Next: ADR-0017 (Rejection of mDNS Discovery) ➡️](adr-0017-rejection-of-mdns-printer-discovery.md)
