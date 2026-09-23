<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0002: Native AOT First Compatibility and Zero Reflection

> **Navigation:** [⬅️ ADR-0001 (Package Segregation)](adr-0001-package-segregation-and-satellite-architecture.md) | [ADR Index](README.md) | [Next: ADR-0003 (Result Pattern) ➡️](adr-0003-result-pattern-over-exceptions.md)

---

## Status
**Accepted** (2026-09-03)

## Context
In edge computing deployments (Raspberry Pi, industrial mini-PCs, handheld POS terminals) and ultra-lightweight Linux containers, ahead-of-time static compilation (Native AOT) in modern .NET provides major operational advantages: sub-millisecond instant startup, minimal RAM consumption, and self-contained executables running without pre-installing the .NET Runtime on the host.

Many competing libraries rely on dynamic reflection, runtime serializers, legacy Windows APIs, or runtime JIT code emission, preventing execution under Native AOT profiles or triggering runtime crashes due to assembly trimming.

## Decision
1. All repository library projects (`src/`) compile with `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`.
2. Dynamic reflection is strictly prohibited in hot execution paths and serializers.
3. No unpreserved types or trim-incompatible APIs are used.
4. The `SignalR` package is segregated and documents its AOT boundaries based on ASP.NET Core SignalR Server framework capabilities.

## Related Decisions
- **Complements:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Package segregation).
- **Constrains:** [ADR-0004](adr-0004-memory-zero-copy-evolution.md) (Zero-copy), [ADR-0009](adr-0009-decoupled-satellite-imaging-packages.md) (Satellite imaging without SkiaSharp).
- **Motivates Rejections:** [ADR-0012](adr-0012-rejection-of-local-zpl-rendering-engine.md) (Rejection of ZPL renderer), [ADR-0016](adr-0016-rejection-of-windows-gdi-system-drawing.md) (Rejection of GDI+ / System.Drawing).

## Consequences
### Positive
- Guaranteed runtime reliability for POS applications and edge print agents compiled with `PublishAot=true`.
- Minimal memory footprint on resource-constrained embedded hardware.

### Negative
- Architectural constraints: no reflection-based metadata inspection or dynamic command dispatch.

---

> **Navigation:** [⬅️ ADR-0001 (Package Segregation)](adr-0001-package-segregation-and-satellite-architecture.md) | [ADR Index](README.md) | [Next: ADR-0003 (Result Pattern) ➡️](adr-0003-result-pattern-over-exceptions.md)
