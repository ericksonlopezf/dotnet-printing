<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0011: Decorator Pattern for Transient Resilience and Retries

> **Navigation:** [⬅️ ADR-0010 (Structured Observability)](adr-0010-structured-logging-and-observability.md) | [ADR Index](README.md) | [Next: ADR-0012 (Rejection of Local ZPL Renderer) ➡️](adr-0012-rejection-of-local-zpl-rendering-engine.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Physical thermal and label printers experience short transient connection interruptions: hardware network buffers saturate for 500 ms, network switches failover, or a cashier closes the printer cover triggering a TCP link reset. In these scenarios, a single transient connection glitch should not abort the point-of-sale transaction.

We evaluated introducing external dependencies like `Polly` versus hardcoding retries inside `TcpPrinterClient`.

## Decision
1. External dependencies like Polly are excluded from the core to maintain minimal binary footprint and avoid package version conflicts.
2. Implement the **Decorator pattern** via `ResilientPrinterClient`, which implements `IPrinterClient` and wraps any inner client.
3. Implement lightweight **exponential backoff with jitter ceiling**:
   - Retries exclusively on `ErrorType.Unavailable` (timeouts, socket reset, COM port contention).
   - **Does NOT retry** on deterministic errors such as `ErrorType.Validation` (e.g., null document or empty printer name).
   - Honors caller `CancellationToken` on every retry attempt and backoff delay.
4. Provide a fluent extension method: `client.WithRetry(options => { ... })`.
5. Use `TimeProvider` as an abstraction for time, enabling 100% deterministic, instant unit testing without real delays.

## Related Decisions
- **Derived from:** [ADR-0003](adr-0003-result-pattern-over-exceptions.md) (Result pattern enables inspecting errors without catch blocks) and [ADR-0010](adr-0010-structured-logging-and-observability.md) (Emits warning events during retry attempts).
- **Concludes active architectural foundation phases and precedes systematic domain discards (ADR-0012 to ADR-0017).**

## Consequences
- Transparent and composable resilience across any current or future transport driver.
- Zero external package dependencies.
- High testability and Native AOT compatibility.

---

> **Navigation:** [⬅️ ADR-0010 (Structured Observability)](adr-0010-structured-logging-and-observability.md) | [ADR Index](README.md) | [Next: ADR-0012 (Rejection of Local ZPL Renderer) ➡️](adr-0012-rejection-of-local-zpl-rendering-engine.md)
