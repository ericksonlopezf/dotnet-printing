<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0014: Rejection of Printing via Samba/SMB Network Shares

> **Navigation:** [⬅️ ADR-0013 (Rejection of Bluetooth)](adr-0013-rejection-of-bluetooth-transport.md) | [ADR Index](README.md) | [Next: ADR-0015 (Rejection of ZPL Template Engine) ➡️](adr-0015-rejection-of-built-in-zpl-template-engine.md)

---

## Status
**Rejected** (2026-09-03)

## Context
In legacy Windows NT / Windows 98 architectures, a common pattern for sharing receipt printers involved sharing the device as a Windows network UNC share (e.g., `\\server\shared_printer`) and copying raw text files directly across SMB/CIFS.

We evaluated whether `EricksonLopez.Printing` should implement a `SambaPrinterClient` or `FileSharePrinterClient`.

## Decision
Implementing print clients based on Samba or SMB network shares is **formally rejected**.

## Rationale
1. **High-Risk Legacy Pattern:** Direct writing to UNC printer queues over SMB relies on Windows domain NTLM/Kerberos authentication, SMB ports (445/139) that are widely blocked in modern enterprise networks for security reasons, and OS print spooler services.
2. **Cross-Platform Incompatibility:** Handling UNC paths and Windows credentials from Linux containers requires CIFS kernel mounts or unmanaged third-party SMB client libraries incompatible with Native AOT.
3. **Superior Standard Alternative:** Modern network printers expose a raw TCP socket on port 9100 (JetDirect / AppSocket), operating universally across OS platforms with zero-trust networking compatibility and sub-10ms transmission latency.

## Related Decisions
- **Derived from:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Lightweight core without SMB networking dependencies) and [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Cross-platform Native AOT without legacy OS libraries).

---

> **Navigation:** [⬅️ ADR-0013 (Rejection of Bluetooth)](adr-0013-rejection-of-bluetooth-transport.md) | [ADR Index](README.md) | [Next: ADR-0015 (Rejection of ZPL Template Engine) ➡️](adr-0015-rejection-of-built-in-zpl-template-engine.md)
