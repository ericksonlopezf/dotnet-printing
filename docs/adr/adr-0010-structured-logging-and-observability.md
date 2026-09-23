<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0010: Structured Telemetry with Null-Logger Fallback

> **Navigation:** [⬅️ ADR-0009 (Decoupled Satellite Imaging)](adr-0009-decoupled-satellite-imaging-packages.md) | [ADR Index](README.md) | [Next: ADR-0011 (Resilience Decorator) ➡️](adr-0011-transient-resilience-and-retry-decorator.md)

---

## Status
**Accepted** (2026-09-03)

## Context
In distributed production deployments (such as retail chains with hundreds of POS terminals or logistics warehouses with dozens of Zebra printers), diagnosing printing failures (network timeouts, socket disconnections, serial port permission errors) is vital.

However, mandating an `ILogger<T>` instance in public client constructors forces manual callers (`new TcpPrinterClient(...)` in scripts or test utilities) to pass mocks or `NullLogger.Instance`, deteriorating Developer Experience.

## Decision
1. Add an optional `ILogger<T>? logger = null` parameter to public constructors of `TcpPrinterClient`, `SerialPrinterClient`, and `HubPrinterDispatcher`.
2. Emit structured log events using standardized Event IDs:
   - `1001`: TCP connection established.
   - `1002`: Bytecode transmission completed successfully.
   - `1003`: Hardware or socket failure with structured error details.
   - `1004`–`1006`: Corresponding events for serial COM ports.
3. Update DI extension methods (`AddTcpPrinter`, `AddSerialPrinter`, `AddPrintingSignalR`) to resolve and inject registered `ILogger<T>` automatically.
4. When `logger` is null, execution proceeds without allocations or overhead via null-conditional logging (`_logger?.Log*`).

## Related Decisions
- **Derived from:** [ADR-0008](adr-0008-di-client-lifecycles-and-serial-concurrency.md) (DI injection and optional dependencies).
- **Constrains:** [ADR-0011](adr-0011-transient-resilience-and-retry-decorator.md) (Resilience decorator emits structured warning events during retries).

## Consequences
- Industrial-grade observability without configuration overhead for ASP.NET Core and Microsoft Generic Host consumers.
- Zero friction for callers instantiating clients directly without a DI container.

---

> **Navigation:** [⬅️ ADR-0009 (Decoupled Satellite Imaging)](adr-0009-decoupled-satellite-imaging-packages.md) | [ADR Index](README.md) | [Next: ADR-0011 (Resilience Decorator) ➡️](adr-0011-transient-resilience-and-retry-decorator.md)
