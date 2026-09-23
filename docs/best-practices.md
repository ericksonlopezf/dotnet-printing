<!-- Copyright © Erickson Lopez. MIT License. -->
# Best Practices & Physical Safety Guide — EricksonLopez.Printing

Direct communication with thermal POS receipt printers and industrial label printers presents unique hardware challenges that differ substantially from traditional web or database software. A software bug in a printing pipeline can cause physical paper waste, mechanical cutter jams, or duplicate fiscal tax invoicing.

This guide details recommended patterns, configuration rules, and memory practices for mission-critical production deployments.

---

## 1. The Golden Rule of Physical Idempotency

> [!CAUTION]
> **Unlike a relational database transaction or an idempotent HTTP GET endpoint, physical printing hardware HAS NO ROLLBACK CAPABILITY.**  
> Once the thermal printhead heats the paper or the blade cuts the receipt, the physical action is irreversible.

### The Mid-Stream Transmission Drop Dilemma
If a network disconnect occurs **while** bytes are actively being streamed over a TCP socket:
- The printer may have already received 80% of the ticket, advanced the roll, and triggered the cutter.
- If an automated retry policy blindly re-sends the document from byte 0, the customer receives **two physical tickets**, potentially duplicating an order in the kitchen or printing two fiscal invoices with the same receipt number.

### Configuration Invariants
- By default, `ResilientPrinterOptions.RetryOnTransmissionError` is strictly set to `false`.
- **Keep it `false`** for commercial receipts, kitchen tickets, and documents carrying monetary or fiscal value.
- Only consider setting `RetryOnTransmissionError = true` if:
  1. The target printer firmware enforces hardware-level job deduplication via unique Job IDs.
  2. The document is an informational reprint or internal audit trail where physical duplicates cause no business impact.

---

## 2. Choosing the Right Client: Ephemeral vs Persistent Connection Pooling

| Operational Dimension | `TcpPrinterClient` (Ephemeral) | `PooledTcpPrinterClient` (Persistent) |
|---|---|---|
| **Socket Lifecycle** | Opens and closes a TCP socket per `PrintAsync` invocation. | Maintains a continuous, open TCP socket across jobs. |
| **Concurrency Model** | Each calling thread attempts to open its own concurrent socket. | Enqueues and serializes calls via an internal `SemaphoreSlim(1, 1)`. |
| **Hardware Collision Risk** | High if the printer firmware only supports a single active TCP connection (common on POS receipt printers). | Zero. Jobs are transmitted strictly one after another without byte interleaving. |
| **TIME_WAIT Socket Exhaustion** | Possible when transmitting dozens of jobs per minute on Windows/Linux servers. | Non-existent (re-uses existing connection). |
| **Optimal Use Case** | Shared office printers with low, intermittent traffic. | **Point-of-Sale (POS) checkouts, self-service kiosks, and automated distribution fulfillment lines.** |

---

## 3. Defense-in-Depth with `safeMode`

In `EscPosBuilder`, the constructor takes a `bool safeMode` parameter:

```csharp
// Recommended configuration for user-supplied data and public inputs
using var builder = new EscPosBuilder(safeMode: true);
```

- **`safeMode: true` (Default)**:
  - Sanitizes unescaped control sequences.
  - Strictly prohibits `.Raw()` calls that could reconfigure printer non-volatile memory (NVRAM) or switch code pages maliciously.
- **`safeMode: false`**:
  - Required when transmitting binary raster images (`.Image()`, `.RasterImage()`) or raw firmware escape sequences.
  - Ensure image data is authenticated and validated before disabling safe mode.

---

## 4. Memory Management & Object Lifecycles

### Always Dispose `EscPosBuilder`
`EscPosBuilder` rents memory streams from `RecyclableMemoryStreamManager` to eliminate Large Object Heap (LOH) allocations. Always dispose the builder or use a `using` block:

```csharp
using var builder = new EscPosBuilder();
builder.Initialize().Line("Sample").Feed(2).Cut();
var document = builder.Build("Ticket-01");
```

### Use `Build()` with Meaningful Document Names and Idempotency Keys
Always provide a descriptive `documentName` and `idempotencyKey` when calling `builder.Build(documentName, idempotencyKey)`:

```csharp
var document = builder.Build(
    documentName: $"Receipt-{order.Id}",
    idempotencyKey: order.TransactionId);
```

---

## 5. Mandatory `CancellationToken` Propagation

Never pass `CancellationToken.None` in production ASP.NET Core endpoints or background workers:

```csharp
// RECOMMENDED: Aborts immediately if client disconnects or request times out
await printer.PrintAsync(document, httpContext.RequestAborted);

// DANGEROUS: Sockets can block thread-pool threads indefinitely on frozen network interfaces
await printer.PrintAsync(document, CancellationToken.None);
```

---

## 6. Observability & Structured Logging Telemetry

Utilize standardized `PrintingEventIds` to configure alerting rules in Grafana, Seq, Datadog, or Azure Application Insights:

```csharp
// Standard Event IDs emitted by transport clients:
// PrintingEventIds.TcpPrintStarted   (ID 1001) - Job dispatch initiated
// PrintingEventIds.TcpPrintSuccess   (ID 1002) - Byte stream confirmed by socket buffer
// PrintingEventIds.TcpPrintFailed    (ID 1003) - Immediate hardware / socket connection alert
// PrintingEventIds.RetryAttempt      (ID 1010) - Transient retry warning (Wi-Fi jitter)
// PrintingEventIds.RetryExhausted    (ID 1011) - Critical alert: all retries failed
```
