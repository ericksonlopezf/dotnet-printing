<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0020: Physical Idempotency and Non-Retryable Transmission Errors

> **Navigation:** [⬅️ ADR-0019 (Serial Transport Segregation)](adr-0019-segregation-of-serial-rs232-transport-satellite.md) | [ADR Index](README.md) | [ADR-0021 (Pooled TCP Transport) ➡️](adr-0021-pooled-tcp-transport-for-high-throughput.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Unlike idempotent HTTP operations (e.g. `GET` or `PUT`) or message queues with deduplication mechanisms, physical printers consume tangible consumable resources (thermal paper rolls, resin transfer ribbons, synthetic labels) and execute irreversible mechanical actions (partial or full guillotine cuts, drawer solenoid kick pulses, RFID tag encoding).

When an error occurs during `PrintAsync`, network resilience patterns (like Polly or naive retry loops) may retransmit the document. However, if bytes have already entered the printer's internal receive buffer before the connection drops, retrying the entire document prints duplicate receipts, burns unnecessary thermal paper, or jams the feed mechanism.

We evaluated how the suite should handle print job idempotency and distinguish between connection-level retryable errors and mid-stream non-retryable transmission errors.

## Decision
We establish physical idempotency guidelines and distinct error taxonomy:
1. `IPrintDocument` defines the `bool IsIdempotent` property (defaults to `false` for cut/cash-drawer sequences; optionally `true` for read-only status query labels or idempotent test vouchers) and an optional `string? IdempotencyKey`.
2. The retry decorator `ResilientPrinterClient` distinguishes error categories via `PrintingErrorCodes`:
   - **Retryable Errors:** Connection establishment failures (`Printer.SocketError`, initial `Printer.Timeout` prior to byte transmission).
   - **Non-Retryable Errors:** Mid-stream transmission failures (`Printer.TransmissionError`), cancellation (`Printer.Canceled`), or client disposal (`ObjectDisposedException`).
3. If a transmission fails after bytes have been sent, the operation returns a failed `Result` without automatic retransmission, leaving reconciliation to the host application.

## Rationale
1. **Preventing Physical Resource Duplication:** Retrying a 50-line fiscal receipt that dropped during byte 45 causes the printer to print the first 45 lines, feed, and then print all 50 lines again, generating confusing fiscal discrepancies and wasting paper.
2. **Mechanical Hazard Prevention:** Rapid successive retransmissions of cash drawer kick pulses (`ESC p`) can overheat solenoid coils in high-volume retail environments.
3. **Auditability & Reconciliation:** Emitting a distinct `Printer.TransmissionError` allows the upper business layer (POS workstation or ERP dispatcher) to prompt the cashier or operator to physically inspect the printer feed before deciding whether to reprint.

## Consequences
### Positive
- Prevents duplicate receipts, duplicate shipping labels, and wasted ribbon/paper stock.
- Eliminates mechanical hardware stress caused by repeated solenoid kicks.
- Provides precise failure diagnostics via `Result.Error.Code` and `PrintingErrorCodes`.

### Negative
- Applications must handle `Printer.TransmissionError` gracefully rather than expecting transparent automatic retries on all network anomalies.

## Related Decisions
- **Derived from:** [ADR-0003](adr-0003-result-pattern-over-exceptions.md) (Result pattern over exceptions for hardware errors).
- **Derived from:** [ADR-0011](adr-0011-transient-resilience-and-retry-decorator.md) (Resilient retry decorator with backoff and jitter).
- **Informs:** [ADR-0004](adr-0004-memory-zero-copy-evolution.md) (Byte payload inspection and streaming).

---

> **Navigation:** [⬅️ ADR-0019 (Serial Transport Segregation)](adr-0019-segregation-of-serial-rs232-transport-satellite.md) | [ADR Index](README.md) | [ADR-0021 (Pooled TCP Transport) ➡️](adr-0021-pooled-tcp-transport-for-high-throughput.md)
