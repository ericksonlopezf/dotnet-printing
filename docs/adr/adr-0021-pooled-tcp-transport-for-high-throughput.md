<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0021: Persistent Pooled TCP Transport for High-Throughput Dedicated Printers

> **Navigation:** [⬅️ ADR-0020 (Physical Idempotency & Non-Retryable Errors)](adr-0020-physical-idempotency-and-non-retryable-transmission-errors.md) | [ADR Index](README.md)

---

## Status
**Accepted** (2026-09-23) — *Amends and supersedes the absolute rejection of socket pooling in [ADR-0018](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md) by introducing an opt-in, single persistent connection transport for dedicated printing endpoints.*

---

## Context
[ADR-0018](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md) mandated an ephemeral connect-write-close lifecycle for `TcpPrinterClient` to prevent multi-station lockout on shared thermal printers. While this premise remains valid for intermittent POS stations sharing a physical printer, high-throughput production environments introduce contrary constraints:

1. **High-Rate Automated Logistics:** Automated warehouse dispatch, parcel sorting conveyors, and automated kitchen display print daemons transmit tens of documents per second to a single dedicated printer.
2. **Socket `TIME_WAIT` Exhaustion:** Establishing and closing dozens of ephemeral TCP connections per second rapidly consumes ephemeral client ports, placing sockets into `TIME_WAIT` (up to 120–240 seconds by OS default), causing connection failures (`SocketError.AddressAlreadyInUse` or socket starvation).
3. **Latency Overhead:** The TCP 3-way handshake roundtrip (1–5 ms per connection) introduces latency accumulation in synchronous transaction pipelines.

We needed a dedicated transport that eliminates per-job connection overhead while mitigating the risks of half-open sockets and embedded firmware buffer locks identified in ADR-0018.

---

## Decision
We introduce `PooledTcpPrinterClient` as a first-class, opt-in transport implementation alongside `TcpPrinterClient`:

1. **Dual Transport Strategy:**
   - **`TcpPrinterClient` (Ephemeral):** Continues to follow ADR-0018. Default choice for shared multi-terminal receipt printers and low-frequency desktop printing.
   - **`PooledTcpPrinterClient` (Pooled):** Opt-in choice for dedicated, high-frequency printing backends, automated fulfillment lines, and centralized microservices.

2. **Single Persistent Connection Architecture:**
   - The implementation maintains **one** persistent `TcpClient` instance (`private TcpClient? _client`) per `PooledTcpPrinterClient` instance.
   - A `SemaphoreSlim(1, 1)` gate (`_gate`) serializes concurrent callers so that only one print job writes to the socket at a time, preventing interleaved byte streams.
   - This is **not** a multi-connection channel pool. Print jobs are queued by the semaphore and executed sequentially over the single persistent socket.

3. **Proactive Dead Socket Eviction & Health Checks:**
   - Prior to reusing the persistent socket, `PooledTcpPrinterClient` inspects connection health:
     ```csharp
     bool isDisconnected = socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
     ```
   - Dead or severed sockets are discarded and a new connection is established on demand (`EnsureConnectedAsync`) without failing the caller.

4. **`WarmupAsync(CancellationToken)` Pre-Warming:**
   - Exposes `WarmupAsync(CancellationToken)` to pre-establish the TCP connection during application startup or container readiness probes.
   - This eliminates first-print TCP handshake latency in production deployments using IHostedService or WebApplication startup hooks.

5. **`IAsyncDisposable` + `IDisposable` Lifecycle:**
   - Implements both `IAsyncDisposable` and `IDisposable` to ensure the underlying `TcpClient` and `NetworkStream` are released deterministically when the DI container disposes the Singleton registration.

6. **DI First-Class Integration:**
   - Registered via `services.AddPooledTcpPrinter(...)` and `services.AddKeyedPooledTcpPrinter(...)`.

> **Design Note on Pool Architecture:** An earlier draft of this ADR described a bounded `Channel<TcpClient>` multi-connection pool with concurrent writers. The final implementation uses a simpler and safer design: a **single persistent `TcpClient`** with a `SemaphoreSlim(1,1)` gate serializing all concurrent callers. This eliminates the complexity of connection pool balancing while still eliminating per-job TCP handshake overhead. True parallel write throughput is not supported by design — thermal printers serialize byte streams in firmware regardless.

---

## Consequences

### Positive
- **Reduced Handshake Overhead:** Pre-connected socket transmits payloads without incurring TCP 3-way handshake roundtrips for subsequent jobs.
- **`TIME_WAIT` Elimination:** No ephemeral port recycling under continuous high-frequency printing load.
- **Architectural Flexibility:** Applications choose the exact connection lifecycle that fits their physical network topology (ephemeral vs pooled).
- **Thread Safety:** `SemaphoreSlim(1,1)` prevents concurrent access to the socket, which is not thread-safe by design.

### Negative / Trade-offs
- **Dedicated Device Lock:** A pooled client holds a persistent connection open, meaning other external applications cannot print to that physical printer while the connection is held.
- **Sequential Throughput Only:** The single-connection + semaphore design serializes all print jobs. True parallel multi-connection throughput is not supported.
- **Resource Management:** Requires proper disposal of the `PooledTcpPrinterClient` instance (enforced via Singleton DI lifecycle and `IAsyncDisposable`).

---

## Related Decisions
- **Supersedes:** [ADR-0018](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md) (replaces absolute rejection with dual-transport coexistence).
- **Related to:** [ADR-0008](adr-0008-di-client-lifecycles-and-serial-concurrency.md) (DI Singleton registration pattern).
- **Related to:** [ADR-0020](adr-0020-physical-idempotency-and-non-retryable-transmission-errors.md) (Socket error recovery and transmission boundaries).

---

> **Navigation:** [⬅️ ADR-0020 (Physical Idempotency & Non-Retryable Errors)](adr-0020-physical-idempotency-and-non-retryable-transmission-errors.md) | [ADR Index](README.md)
