<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0019: Segregation of Serial RS-232 Transport into Dedicated Satellite

> **Navigation:** [⬅️ ADR-0018 (Connection Pooling Lifecycle)](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md) | [ADR Index](README.md) | [ADR-0020 (Physical Idempotency & Retries) ➡️](adr-0020-physical-idempotency-and-non-retryable-transmission-errors.md)

---

## Status
**Accepted** (2026-09-03)

## Context
While TCP/IP (Raw port 9100) is the dominant enterprise printing protocol, legacy POS terminals, industrial scales, and specialized label dispensers rely heavily on direct serial interfaces (RS-232 / USB Virtual COM Port).

Supporting serial communication in .NET requires `System.IO.Ports`, a package with OS-specific platform implementations, pinvoke bindings, and unmanaged serial port handle lifecycles.

We evaluated whether to embed serial communication into the root `EricksonLopez.Printing` package or isolate it into a dedicated satellite library (`EricksonLopez.Printing.Serial`).

## Decision
We segregate all serial port functionality into the dedicated satellite package `EricksonLopez.Printing.Serial`:
1. The core `EricksonLopez.Printing` assembly contains zero references to `System.IO.Ports`.
2. `SerialPrinterClient` implements `IPrinterClient` within the satellite package.
3. Applications requiring serial connectivity explicitly install `EricksonLopez.Printing.Serial`.

## Rationale
1. **Core Runtime Purity & Trimmability:** `System.IO.Ports` depends on native runtime libraries (`libdl`, `libc`, Win32 `CreateFile` / `SetCommState`). Bundling it into the core package forces every serverless, microservice, or cloud-hosted API using only TCP printing to pull platform-dependent binaries, complicating container image builds and Native AOT dead-code trimming.
2. **OS Differences and Hardware Lock Invariants:** Serial communication mandates exclusive OS-level device locks (e.g. `COM1` on Windows, `/dev/ttyUSB0` or `/dev/ttyS0` on Linux). Concurrent process access results in `UnauthorizedAccessException`. Isolating this into a satellite allows dedicated concurrency synchronization rules (`SemaphoreSlim` per COM port) without polluting the TCP driver surface.
3. **Selective Deployment:** Cloud-native applications transmitting print payloads across subnets or edge gateways have no serial hardware. They benefit from a minimal attack surface and leaner dependency footprint.

## Consequences
### Positive
- Core package remains 100% managed, Native AOT trimmable, and free of platform-specific unmanaged library bindings.
- Users deploying only TCP network printers avoid the `System.IO.Ports` dependency entirely.
- Dedicated configuration options (`SerialPrinterClientOptions`: BaudRate, Parity, DataBits, StopBits, Handshake) reside only where needed.

### Negative
- Developers requiring both TCP and Serial transports must reference two separate packages.

## Related Decisions
- **Derived from:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Satellite package segregation).
- **Derived from:** [ADR-0002](adr-0002-native-aot-first-and-zero-reflection.md) (Native AOT compatibility and zero platform bloat).
- **Informs:** [ADR-0008](adr-0008-di-client-lifecycles-and-serial-concurrency.md) (Serial client lifetime and port contention).

---

> **Navigation:** [⬅️ ADR-0018 (Connection Pooling Lifecycle)](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md) | [ADR Index](README.md) | [ADR-0020 (Physical Idempotency & Retries) ➡️](adr-0020-physical-idempotency-and-non-retryable-transmission-errors.md)
