<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0017: Rejection of Automatic Printer Discovery (mDNS / Bonjour)

> **Navigation:** [⬅️ ADR-0016 (Rejection of Windows GDI)](adr-0016-rejection-of-windows-gdi-system-drawing.md) | [ADR Index](README.md) | [ADR-0018 (Connection Pooling) ➡️](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md)

---

## Status
**Rejected** (2026-09-03)

## Context
Many modern network printers broadcast availability on local subnets via multicast DNS discovery protocols (mDNS / Bonjour / ZeroConf / WS-Discovery).

We evaluated introducing an auto-discovery network scanner into the core library that probes subnets and returns detected printers.

## Decision
Including network printer auto-discovery capabilities is **formally rejected**.

## Rationale
1. **Architectural Responsibility Boundaries:** The core library responsibility is communicating with a specified print destination (`Host` + `Port` or `PortName`) and dispatching bytecode reliably. Discovering and provisioning printer endpoints belongs to network management and infrastructure configuration layers.
2. **Unreliable Enterprise Multicast:** Enterprise routers, segmented VLANs (separating POS terminals from management subnets), and corporate firewalls routinely drop UDP multicast traffic, making mDNS discovery erratic and non-viable in enterprise environments.
3. **Dependency and Runtime Overhead:** Requires background UDP listeners, broadcast socket permissions, and custom firewall configuration in containerized deployments.

## Related Decisions
- **Derived from:** [ADR-0001](adr-0001-package-segregation-and-satellite-architecture.md) (Strict core boundaries: separation between network provisioning and bytecode transmission drivers).

---

> **Navigation:** [⬅️ ADR-0016 (Rejection of Windows GDI)](adr-0016-rejection-of-windows-gdi-system-drawing.md) | [ADR Index](README.md) | [ADR-0018 (Connection Pooling) ➡️](adr-0018-connection-pooling-and-persistent-socket-lifecycle.md)
