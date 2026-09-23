<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0013: Rejection of Bluetooth Transport in Core Suite

> **Navigation:** [⬅️ ADR-0012 (Rejection of Local ZPL Renderer)](adr-0012-rejection-of-local-zpl-rendering-engine.md) | [ADR Index](README.md) | [Next: ADR-0014 (Rejection of Samba / SMB) ➡️](adr-0014-rejection-of-samba-and-network-file-shares.md)

---

## Status
**Rejected** (2026-09-03)

## Context
Some mobile receipt printers and handheld terminals use Bluetooth interfaces (Bluetooth Classic SPP or Bluetooth Low Energy - BLE) to pair with handheld terminals or mobile POS devices.

We analyzed introducing a `BluetoothPrinterClient` into the core `EricksonLopez.Printing` package.

## Decision
Including Bluetooth transport within the core library suite is **formally rejected**.

## Rationale
1. **Coupling to Fragmented Native OS APIs:** The Bluetooth stack is not standardized in modern .NET. Windows requires WinRT/UWP APIs (`Windows.Devices.Bluetooth`), Linux requires the BlueZ daemon via D-Bus, and macOS requires CoreBluetooth. No unified, cross-platform, Native AOT compatible abstraction exists in the .NET base class library.
2. **Device Pairing & Permission Lifecycle:** Managing physical pairing, service discovery, RF range disconnections, and operating system permission prompts falls outside the scope of a raw printer bytecode transmission driver.
3. **Market Architecture Trend:** Modern commercial and SaaS/Edge POS architectures utilize Ethernet/Wi-Fi with standard TCP sockets (port 9100) or USB Virtual COM (Serial RS-232), both of which are fully supported out-of-the-box.

## Related Decisions
- **Derived from:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Core package must avoid OS-specific native dependencies) and [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Cross-platform and Native AOT guarantees).

---

> **Navigation:** [⬅️ ADR-0012 (Rejection of Local ZPL Renderer)](adr-0012-rejection-of-local-zpl-rendering-engine.md) | [ADR Index](README.md) | [Next: ADR-0014 (Rejection of Samba / SMB) ➡️](adr-0014-rejection-of-samba-and-network-file-shares.md)
