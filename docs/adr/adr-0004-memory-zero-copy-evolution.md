<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0004: Evolution Toward Zero-Copy via `ReadOnlyMemory<byte>`

> **Navigation:** [⬅️ ADR-0003 (Result Pattern)](adr-0003-result-pattern-over-exceptions.md) | [ADR Index](README.md) | [Next: ADR-0005 (Single-Threaded Builder) ➡️](adr-0005-builder-thread-safety-invariants.md)

---

## Status
**Accepted** (2026-09-03)

## Context
In version v1.0.0, the `IPrintDocument` interface exposed only:
```csharp
byte[] GetBytes();
```
Returning a `byte[]` array frequently forces defensive copies (e.g., `_buffer.ToArray()`) to prevent callers from mutating the builder's internal state. In high-volume industrial printing environments with thousands of labels or receipts per minute, repeated GC allocations create unnecessary pressure.

Conversely, drastically altering the signature of `GetBytes()` in `IPrintDocument` would break binary and source backward compatibility for existing v1.0.0 consumers.

## Decision
1. Retain `byte[] GetBytes()` on `IPrintDocument` throughout the v1.x lifecycle to preserve full binary and source compatibility.
2. Add a Default Interface Method (DIM) to `IPrintDocument`:
   ```csharp
   ReadOnlyMemory<byte> GetMemory() => GetBytes();
   ```
3. Implement `GetMemory()` with zero copy on concrete implementations (`RawPrintDocument`), avoiding redundant array allocations.
4. Establish the foundation for `GetMemory()` to become the primary zero-copy contract in major version v2.0.0.

## Related Decisions
- **Derived from:** [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (AOT performance and GC pressure reduction).
- **Complements:** [ADR-0006](adr-0006-zpl-builder-resource-management.md) (Resource management in command builders).

## Consequences
### Positive
- 100% backward compatible: zero breaking changes for existing consumers.
- Enables advanced high-performance workflows where memory buffers are processed as spans or slices without allocations.

### Negative
- During the v1.x lifecycle, implementations that only override `GetBytes()` still allocate if the default DIM is invoked.

---

> **Navigation:** [⬅️ ADR-0003 (Result Pattern)](adr-0003-result-pattern-over-exceptions.md) | [ADR Index](README.md) | [Next: ADR-0005 (Single-Threaded Builder) ➡️](adr-0005-builder-thread-safety-invariants.md)
