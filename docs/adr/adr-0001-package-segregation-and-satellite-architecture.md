<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0001: Package Segregation and Satellite Architecture

> **Navigation:** `[Home]` | [ADR Index](README.md) | [Next: ADR-0002 (Native AOT First) ➡️](adr-0002-native-aot-first-and-zero-reflection.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Printing in industrial and Point-of-Sale (POS) applications spans multiple protocols (ESC/POS binary for receipts, ZPL II for Zebra labels), various physical transports (TCP socket port 9100, Serial RS-232 / USB Virtual COM), real-time bidirectional communication (SignalR for edge agents), and optional CPU-intensive processing (image conversion and hardware status monitoring).

Packaging all these capabilities into a single monolithic artifact would introduce heavyweight transitive dependencies (ASP.NET Core SignalR, SkiaSharp, port communication libraries), forcing lightweight microservices and edge agents to download unnecessary binaries, while expanding the attack surface and risking AOT incompatibilities.

## Decision
A modular architecture is established, segmented into highly cohesive NuGet packages with unidirectional coupling:

1. **`EricksonLopez.Printing` (Core Tier 0):** Core contracts (`IPrintDocument`, `IPrinterClient`), raw TCP transport drivers (`TcpPrinterClient`, `PooledTcpPrinterClient`), resilience decorator (`ResilientPrinterClient`), and DI extensions. Zero web or imaging dependencies.
2. **`EricksonLopez.Printing.EscPos`:** Fluent builder engine for Epson ESC/POS binary command sequences.
3. **`EricksonLopez.Printing.Zpl`:** Fluent builder engine for Zebra Programming Language (ZPL II) industrial labels.
4. **`EricksonLopez.Printing.SignalR`:** Hub and print dispatcher for cloud SaaS backends routing jobs to distributed local agents.
5. **`EricksonLopez.Printing.EscPos.Imaging` (Satellite):** Monochrome bitmap conversion, dithering algorithms (Threshold, Floyd-Steinberg), and raster bit imaging (`GS v 0`).
6. **`EricksonLopez.Printing.Zpl.Imaging` (Satellite):** Image conversion to ZPL hexadecimal graphic fields (`^GF`).
7. **`EricksonLopez.Printing.EscPos.Status` (Satellite):** Real-time hardware status query and sensor response parsing (`DLE EOT n`).
8. **`EricksonLopez.Printing.Serial` (Satellite Tier 1):** Dedicated RS-232 / Virtual COM port driver (`SerialPrinterClient`) isolated per [ADR-0019](adr-0019-segregation-of-serial-rs232-transport-satellite.md).

> **Architectural Note:** Originally, serial printing was slated for Core Tier 0 in preliminary drafts; it was segregated into its own 8th package via ADR-0019 to prevent pulling `System.IO.Ports` and unmanaged COM port dependencies into cloud containers.

## Related Decisions
- **Constrains:** [ADR-0008](adr-0008-di-client-lifecycles-and-serial-concurrency.md) (DI lifecycles), [ADR-0009](adr-0009-decoupled-satellite-imaging-packages.md) (Satellite imaging).
- **Motivates Rejections:** [ADR-0013](adr-0013-rejection-of-bluetooth-transport.md) (Rejection of Bluetooth in core), [ADR-0014](adr-0014-rejection-of-samba-and-network-file-shares.md) (Rejection of Samba).

## Consequences
### Positive
- Consumers install only the packages strictly required for their specific use case.
- Core remains lightweight, fast, and 100% Native AOT compatible.
- Heavy computational algorithms or complex dependencies do not penalize binary footprint or startup times.

### Negative
- Multiple NuGet packages to version and publish via CI/CD.

---

> **Navigation:** `[Home]` | [ADR Index](README.md) | [Next: ADR-0002 (Native AOT First) ➡️](adr-0002-native-aot-first-and-zero-reflection.md)
