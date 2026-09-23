<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0003: Result Pattern Adoption for Hardware Error Handling

> **Navigation:** [⬅️ ADR-0002 (Native AOT First)](adr-0002-native-aot-first-and-zero-reflection.md) | [ADR Index](README.md) | [Next: ADR-0004 (Zero-Copy Evolution) ➡️](adr-0004-memory-zero-copy-evolution.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Communication with physical printing hardware is inherently prone to transient failures: printers turned off, unplugged network cables, power outages, full hardware buffers, or socket timeouts.

The traditional .NET approach throws exceptions (`SocketException`, `IOException`, `TimeoutException`). However:
1. Exceptions are not part of C# method signatures, forcing callers to guess which `try-catch` blocks to implement.
2. Throwing and catching exceptions incurs performance overhead due to stack trace captures.
3. A failed printer connection is not a catastrophic unexpected software bug; it is an expected operational business state in physical environments.

## Decision
All transport clients (`TcpPrinterClient`, `SerialPrinterClient`, `ResilientPrinterClient`) and the `HubPrinterDispatcher` return a structured `Result<bool>` (backed by `EricksonLopez.Result`):

- On success, returns `Result<bool>.Success(true)`.
- On infrastructure or hardware failure, catches low-level exceptions internally and returns structured `Error.Unavailable(code, description)` (e.g., `Printer.SocketError`, `Printer.Timeout`, `Printer.SerialAccessDenied`).
- On invalid arguments or printer targets, returns `Error.Validation(code, description)`.

## Related Decisions
- **Constrains:** [ADR-0011](adr-0011-transient-resilience-and-retry-decorator.md) (Resilience decorator inspects `ErrorType.Unavailable` without wrapping calls in try-catch).
- **Aligned with:** Ecosystem architectural invariants across `EricksonLopez` libraries.

## Consequences
### Positive
- Compile-time enforcement for consumers to handle failure states without unhandled runtime exceptions.
- Immediate diagnostic telemetry via standard error codes.
- Consistent and predictable behavior across all transport mediums.

### Negative
- External dependency on `EricksonLopez.Result` in v1.x for projects consuming print client APIs.

---

> **Navigation:** [⬅️ ADR-0002 (Native AOT First)](adr-0002-native-aot-first-and-zero-reflection.md) | [ADR Index](README.md) | [Next: ADR-0004 (Zero-Copy Evolution) ➡️](adr-0004-memory-zero-copy-evolution.md)
