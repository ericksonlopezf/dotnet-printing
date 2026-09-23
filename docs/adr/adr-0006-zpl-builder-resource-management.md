<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0006: Resource Management in `ZplBuilder` (No `IDisposable`)

> **Navigation:** [⬅️ ADR-0005 (Single-Threaded Builder)](adr-0005-builder-thread-safety-invariants.md) | [ADR Index](README.md) | [Next: ADR-0007 (Strongly Typed ZPL Fonts) ➡️](adr-0007-strongly-typed-zpl-fonts.md)

---

## Status
**Accepted** (2026-09-03)

## Context
In this ecosystem, `EscPosBuilder` implements `IDisposable` because it wraps an internal `MemoryStream` that benefits from explicit deterministic disposal.

Conversely, `ZplBuilder` accumulates ZPL II commands in an in-memory `StringBuilder`. We evaluated whether `ZplBuilder` should also implement `IDisposable` purely for API symmetry with `EscPosBuilder`.

## Decision
`ZplBuilder` **does NOT implement `IDisposable`**.

## Rationale
1. `StringBuilder` is a purely managed CLR type holding no unmanaged handles, operating system resources, or file pointers.
2. Forcing `IDisposable` on objects with no unmanaged resources violates official .NET Framework Design Guidelines and encourages redundant `using` statements, cluttering caller code.
3. The CLR and Garbage Collector collect `StringBuilder` instances efficiently without manual disposal intervention.

## Related Decisions
- **Derived from:** [ADR-0005](adr-0005-builder-thread-safety-invariants.md) (Transient single-threaded lifecycle).
- **Complements:** [ADR-0007](adr-0007-strongly-typed-zpl-fonts.md) (Ergonomics and type safety of `ZplBuilder` API).

## Consequences
- `ZplBuilder` is instantiated and chained cleanly without requiring `using` blocks.
- Semantic clarity: `IDisposable` in the suite indicates underlying streams or operating system resources.

---

> **Navigation:** [⬅️ ADR-0005 (Single-Threaded Builder)](adr-0005-builder-thread-safety-invariants.md) | [ADR Index](README.md) | [Next: ADR-0007 (Strongly Typed ZPL Fonts) ➡️](adr-0007-strongly-typed-zpl-fonts.md)
