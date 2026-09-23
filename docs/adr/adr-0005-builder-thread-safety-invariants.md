<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0005: Single-Threaded Invariant in Command Builders

> **Navigation:** [⬅️ ADR-0004 (Zero-Copy Evolution)](adr-0004-memory-zero-copy-evolution.md) | [ADR Index](README.md) | [Next: ADR-0006 (ZplBuilder Resource Management) ➡️](adr-0006-zpl-builder-resource-management.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Print document builders (`EscPosBuilder` and `ZplBuilder`) rely on mutable in-memory accumulators (`MemoryStream` and `StringBuilder` respectively).

The architectural question arose whether builders should synchronize internal methods using concurrency primitives (`lock`, `Monitor`, `SemaphoreSlim`) to allow multiple threads to append commands to the same builder concurrently.

## Decision
It is explicitly decided **NOT to add thread safety** to `EscPosBuilder` or `ZplBuilder`.

Builders are transient, sequential construction objects designed to execute within a single thread context while composing a print job (*Single-Threaded Builder pattern*).

## Rationale
1. **Printing Semantics:** Receipts and industrial labels are physical documents with strictly sequential layout (header, detail lines, totals, barcodes, paper cut). Appending commands concurrently across threads would produce scrambled, corrupted, and illegible output.
2. **Performance Penalty:** Adding locks around `Text()`, `Line()`, or `Bold()` would introduce synchronization overhead for 99.9% of normal use cases.
3. **Immutability of Output:** Once `Build()` is called, the resulting document (`IPrintDocument` / `RawPrintDocument`) is fully immutable and safe for concurrent consumption across multiple asynchronous tasks.

## Related Decisions
- **Constrains:** [ADR-0006](adr-0006-zpl-builder-resource-management.md) (Builder lifecycle without `IDisposable`).
- **Complements:** [ADR-0008](adr-0008-di-client-lifecycles-and-serial-concurrency.md) (Distinction between transient builder and singleton/scoped transport clients).

## Consequences
- Builders remain fast, lightweight, and lock-free.
- Documented in the API Reference that builder instances must not be shared across concurrent threads.

---

> **Navigation:** [⬅️ ADR-0004 (Zero-Copy Evolution)](adr-0004-memory-zero-copy-evolution.md) | [ADR Index](README.md) | [Next: ADR-0006 (ZplBuilder Resource Management) ➡️](adr-0006-zpl-builder-resource-management.md)
