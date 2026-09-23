<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0018: Ephemeral Connection Lifecycle over Persistent Socket Pooling

> **Navigation:** [⬅️ ADR-0017 (Rejection of mDNS Discovery)](adr-0017-rejection-of-mdns-printer-discovery.md) | [ADR Index](README.md) | [ADR-0019 (Serial Transport Segregation) ➡️](adr-0019-segregation-of-serial-rs232-transport-satellite.md)

---

## Status
**Superseded by [ADR-0021](adr-0021-pooled-tcp-transport-for-high-throughput.md)** (2026-09-23)

> **Architectural Note:** While this record was superseded regarding the absolute prohibition of socket pooling (now supported via `PooledTcpPrinterClient` in ADR-0021), its design principles remain normative for the standard `TcpPrinterClient` when communicating with shared physical endpoints in multi-station topologies.

## Context
Industrial and POS printers communicating over TCP port 9100 (Raw / JetDirect) feature rudimentary embedded network controllers. Unlike high-throughput HTTP servers, printer network cards typically support only one or a strictly limited number of concurrent TCP connections.

In high-concurrency environments, developers often advocate for connection pooling (keeping TCP sockets open across requests) to avoid three-way handshake overhead and socket exhaustion (`TIME_WAIT`).

We evaluated whether `TcpPrinterClient` should maintain an internal persistent connection pool or adhere to an ephemeral connect-write-close lifecycle per document transmission.

## Decision
We mandate an **ephemeral connection lifecycle** per `PrintAsync` invocation:
1. `TcpPrinterClient` connects to the printer endpoint upon `PrintAsync`.
2. Writes the full payload stream using `Socket.SendAsync` / `NetworkStream.WriteAsync`.
3. Immediately flushes and gracefully disconnects the socket upon transmission completion.
4. Persistent socket pooling in the core transport is rejected.

## Rationale
1. **Physical Device Contention:** Thermal and label printers serve as shared physical endpoints for multiple cash registers or workstation agents. If a single client maintains a persistent socket, other POS stations receive `ECONNREFUSED` or hang waiting for the socket to clear.
2. **Firmware Timeout Instability:** Embedded printer print servers routinely sever idle TCP connections after 15–60 seconds of inactivity without sending `TCP FIN` or `RST` packets, resulting in half-open "zombie" sockets. Attempting to write to pooled dead sockets leads to silent packet drops and transmission stalls.
3. **Buffer Deadlocks:** Certain printer firmware models hold print buffers in volatile memory until connection termination (a `TCP FIN` signals the end-of-job boundary). Persistent connections delay physical cutting or printing until disconnection.
4. **Local Socket Exhaustion Mitigation:** Port 9100 transmission volume in retail/warehousing is measured in tens of documents per minute per printer, well below the ephemeral port exhaustion threshold (~64,000 ports in `TIME_WAIT`).

## Consequences
### Positive
- Prevents printer lockout in multi-terminal retail environments.
- Eliminates keep-alive heartbeats and zombie socket reconnection logic.
- Ensures physical job boundary triggers execute reliably on all firmware revisions.

### Negative
- Each print job incurs a TCP 3-way handshake round-trip (typically 1–3 ms on local LANs).

## Related Decisions
- **Informs:** [ADR-0008](adr-0008-di-client-lifecycles-and-serial-concurrency.md) (DI client lifecycle recommendations).
- **Derived from:** [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Simplicity and minimal state allocation).

---

> **Navigation:** [⬅️ ADR-0017 (Rejection of mDNS Discovery)](adr-0017-rejection-of-mdns-printer-discovery.md) | [ADR Index](README.md) | [ADR-0019 (Serial Transport Segregation) ➡️](adr-0019-segregation-of-serial-rs232-transport-satellite.md)
