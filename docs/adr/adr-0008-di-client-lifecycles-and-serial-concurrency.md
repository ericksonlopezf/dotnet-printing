<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0008: DI Lifecycles and Serial COM Port Contention

> **Navigation:** [⬅️ ADR-0007 (Strongly Typed ZPL Fonts)](adr-0007-strongly-typed-zpl-fonts.md) | [ADR Index](README.md) | [Next: ADR-0009 (Decoupled Satellite Imaging) ➡️](adr-0009-decoupled-satellite-imaging-packages.md)

---

## Status
**Accepted** (2026-09-03)

## Context
Dependency injection extension methods `AddTcpPrinter` and `AddSerialPrinter` register `IPrinterClient` with `Singleton` lifecycle in `IServiceCollection`:
```csharp
services.AddSingleton<IPrinterClient>(sp => new TcpPrinterClient(...));
services.AddSingleton<IPrinterClient>(sp => new SerialPrinterClient(...));
```
For `TcpPrinterClient`, this lifecycle is thread-safe and scalable because each `PrintAsync` invocation allocates a scoped `TcpClient` and `NetworkStream` (*per-call socket*).

However, for `SerialPrinterClient`, physical or virtual COM ports (RS-232 / USB CDC) on operating systems (Windows and Linux `/dev/ttyS*` or `/dev/ttyUSB*`) are exclusive, non-shareable hardware resources. If two threads simultaneously invoke `PrintAsync` on the same `SerialPrinterClient` singleton, the second thread attempts to open the port while it remains locked, throwing `UnauthorizedAccessException` ("Access to the port is denied").

## Decision
1. Maintain default `Singleton` registration in DI extensions for consistency and to avoid redundant configuration overhead.
2. Inject `ILogger<SerialPrinterClient>` into DI factories to log warnings and access-denied errors with clear diagnostic context.
3. Formalize and document the recommended serial concurrency patterns:
   - For single-threaded applications or low-concurrency edge agents, `Singleton` is safe because the port is deterministically opened and closed within a `using var serialPort = new SerialPort(...)` scope.
   - For high-concurrency web backends or multiple POS checkouts sharing a serial port, callers should introduce an in-memory queue (`System.Threading.Channels.Channel`) or wrap the client with `ResilientPrinterClient` configured with exponential backoff retries.

## Related Decisions
- **Derived from:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Core abstraction) and [ADR-0005](adr-0005-builder-thread-safety-invariants.md) (Builder vs transport client distinction).
- **Constrains:** [ADR-0010](adr-0010-structured-logging-and-observability.md) (Automatic `ILogger` injection in DI) and [ADR-0011](adr-0011-transient-resilience-and-retry-decorator.md) (Serial contention mitigation via retries).

## Consequences
- Architectural clarity regarding hardware limitations of physical COM ports.
- Prevents permanent COM port locks via strict `using` block resource management.

---

> **Navigation:** [⬅️ ADR-0007 (Strongly Typed ZPL Fonts)](adr-0007-strongly-typed-zpl-fonts.md) | [ADR Index](README.md) | [Next: ADR-0009 (Decoupled Satellite Imaging) ➡️](adr-0009-decoupled-satellite-imaging-packages.md)
