<!-- Copyright © Erickson Lopez. MIT License. -->
# System Architecture Guide — EricksonLopez.Printing

This guide details the architectural foundations, functional system map, design patterns, and structural diagrams governing the `EricksonLopez.Printing` library ecosystem.

---

## 1. Architectural Vision & Core Invariants

The `EricksonLopez.Printing` ecosystem is engineered around five fundamental principles:

1. **OS Agnosticism & Container-First Execution**:
   Complete elimination of dependencies on proprietary graphics runtimes or OS desktop print spoolers (such as GDI+, `System.Drawing`, or `winspool.drv`), enabling identical execution across Linux (Alpine, Debian, Ubuntu), Docker, Kubernetes, macOS, and Windows.

2. **Zero Runtime Reflection & Native AOT Compatibility**:
   All libraries are engineered for Ahead-of-Time compilation (`PublishAot=true`), with zero unpreserved reflection or dynamic code generation on hot execution paths.

3. **Railway-Oriented Functional Error Handling**:
   Hardware and network I/O operations report outcomes via `Result<bool>` and `Error` types from `EricksonLopez.Result`. Network disconnects and printer hardware faults are modeled as expected domain states rather than unhandled control-flow exceptions.

4. **Zero-Copy Memory Layout & LOH Avoidance**:
   Heavy utilization of `Microsoft.IO.RecyclableMemoryStream`, `ArrayPool<T>.Shared` buffers, `ReadOnlySpan<byte>`, and `ReadOnlyMemory<byte>` avoids Large Object Heap (LOH) fragmentation under continuous high-volume printing.

5. **Decoupled Satellite Package Architecture**:
   Base packages (`EricksonLopez.Printing`, `EricksonLopez.Printing.EscPos`, `EricksonLopez.Printing.Zpl`) maintain minimal dependency footprints. Heavy raster imaging (`Imaging`), hardware status monitoring (`Status`), RS-232 serial ports (`Serial`), and cloud dispatch (`SignalR`) are segregated into opt-in satellite libraries.

---

## 2. Functional System Map

```mermaid
flowchart TD
    subgraph UI_API["Application Entrypoint"]
        APP["Backend Application / POS / Worker Daemon"]
    end

    subgraph CompositionLayer["1. Composition Layer (Builders)"]
        EP_BUILDER["EscPosBuilder\n(Thermal ESC/POS Bytecode)"]
        ZP_BUILDER["ZplBuilder\n(Zebra ZPL II Label Compiler)"]
        IMG_EP["EscPos.Imaging\n(Dithering & Raster GS v 0)"]
        IMG_ZP["Zpl.Imaging\n(Dithering & Graphic Field ^GF)"]

        EP_BUILDER -.-> IMG_EP
        ZP_BUILDER -.-> IMG_ZP
    end

    subgraph DocumentLayer["2. Immutable Document Model"]
        DOC["IPrintDocument\n(DocumentName, IdempotencyKey, IsEmpty, IsIdempotent)"]
        RAW_DOC["RawPrintDocument\n(In-Memory byte[])"]
        ZPL_DOC["ZplPrintDocument\n(Optimized UTF-8 Stream)"]

        EP_BUILDER -->|Build()| RAW_DOC
        ZP_BUILDER -->|Build()| ZPL_DOC
        RAW_DOC -.->|implements| DOC
        ZPL_DOC -.->|implements| DOC
    end

    subgraph ResilienceLayer["3. Resilience & Policy Layer"]
        RETRY["ResilientPrinterClient (Decorator)"]
        POLICY["ResilientPrinterOptions\n(Exponential Backoff + Idempotency Guard)"]
        DOC --> RETRY
        POLICY -.-> RETRY
    end

    subgraph TransportLayer["4. Transport Drivers (IPrinterClient)"]
        TCP_EPHEMERAL["TcpPrinterClient\n(Ephemeral connection per-job)"]
        TCP_POOLED["PooledTcpPrinterClient\n(Persistent connection + SemaphoreSlim)"]
        SERIAL_CLIENT["SerialPrinterClient\n(RS-232 COM Port + SemaphoreSlim)"]

        RETRY -->|Delegates to| TCP_EPHEMERAL
        RETRY -->|Delegates to| TCP_POOLED
        RETRY -->|Delegates to| SERIAL_CLIENT
    end

    subgraph RemoteDispatchLayer["5. Remote Cloud-to-Edge Dispatch"]
        DISP["IHubPrinterDispatcher\n(HubPrinterDispatcher)"]
        HUB["PrinterHub\n(ASP.NET Core SignalR)"]
        EDGE_AGENT["Local Edge Daemon\n(IPrinterHubClient)"]

        DOC --> DISP
        DISP --> HUB
        HUB -->|WebSocket / PrintJobMessage| EDGE_AGENT
        EDGE_AGENT -->|Local PrintAsync| TransportLayer
    end

    subgraph PhysicalHardware["6. Physical Output Devices"]
        PRINTER_NET["Network Printer (Raw Port 9100)"]
        PRINTER_COM["Serial Printer (RS-232 / COM)"]
        STATUS_MODULE["EscPosStatusParser\n(DLE EOT Sensor Queries)"]

        TCP_EPHEMERAL --> PRINTER_NET
        TCP_POOLED --> PRINTER_NET
        SERIAL_CLIENT --> PRINTER_COM
        PRINTER_NET -.-> STATUS_MODULE
        PRINTER_COM -.-> STATUS_MODULE
    end

    APP --> CompositionLayer
```

---

## 3. Sequence & Interaction Diagrams

### 3.1. Persistent Pooled TCP Transmission (`PooledTcpPrinterClient`)

```mermaid
sequenceDiagram
    autonumber
    participant App as Client Application
    participant PooledClient as PooledTcpPrinterClient
    participant Gate as SemaphoreSlim(1,1)
    participant Socket as NetworkStream / TcpClient
    participant Printer as Network Printer (Port 9100)

    App->>PooledClient: PrintAsync(IPrintDocument, CancellationToken)
    alt document.IsEmpty == true
        PooledClient-->>App: Result.Success(true) [Immediate Bypass]
    else document.IsEmpty == false
        PooledClient->>Gate: WaitAsync(CancellationToken)
        activate Gate
        PooledClient->>PooledClient: EnsureConnectedAsync()
        alt Socket null or disconnected
            PooledClient->>Socket: ConnectAsync(Host, Port)
            Socket->>Printer: TCP SYN / ACK Handshake
        end
        PooledClient->>DOC: WriteToAsync(NetworkStream)
        DOC->>Socket: Zero-copy memory streaming
        Socket->>Printer: Raw command byte stream
        PooledClient->>Socket: FlushAsync()
        PooledClient->>Gate: Release()
        deactivate Gate
        PooledClient-->>App: Result.Success(true)
    end
```

### 3.2. Resilience with Exponential Backoff & Physical Idempotency

```mermaid
sequenceDiagram
    autonumber
    participant App as Client Application
    participant Resilient as ResilientPrinterClient
    participant Inner as IPrinterClient
    participant Hardware as Physical Printer

    App->>Resilient: PrintAsync(document)
    loop Attempt 1 to MaxRetries
        Resilient->>Inner: PrintAsync(document)
        Inner->>Hardware: Transmission over socket / serial
        alt Success
            Inner-->>Resilient: Result.Success(true)
            Resilient-->>App: Result.Success(true)
        else Validation Error (ErrorType.Validation)
            Inner-->>Resilient: Error.Validation (Non-transient)
            Note over Resilient: Short-circuits immediately (no retries)
            Resilient-->>App: Result.Failure(Error)
        else Transmission Error mid-stream AND !document.IsIdempotent
            Inner-->>Resilient: Error.Unavailable(Printer.TransmissionError)
            Note over Resilient: INVARIANT: If !IsIdempotent and failed mid-stream,<br/>ABORT to prevent duplicate physical tickets.
            Resilient-->>App: Result.Failure(Error)
        else Transient Connection Error (SocketError / Timeout)
            Inner-->>Resilient: Error.Unavailable(Printer.SocketError)
            Note over Resilient: Calculate delay = InitialDelay * (Multiplier ^ Attempt)
            Resilient->>Resilient: Task.Delay(delay, TimeProvider)
        end
    end
    Note over Resilient: MaxRetries Exhausted
    Resilient-->>App: Result.Failure(LastError)
```

### 3.3. Remote Web-to-Edge Dispatch with ASP.NET Core SignalR

```mermaid
sequenceDiagram
    autonumber
    participant Backend as Cloud Backend (SaaS)
    participant Dispatcher as HubPrinterDispatcher
    participant Hub as PrinterHub
    participant EdgeAgent as Local Store Daemon (IPrinterHubClient)
    participant LocalPrinter as Physical Thermal Printer

    EdgeAgent->>Hub: ConnectAsync("/hubs/printer")
    EdgeAgent->>Hub: RegisterPrinter("Kitchen-01")
    Hub->>Hub: Groups.AddToGroupAsync(ConnId, "printer:KITCHEN-01")

    Backend->>Dispatcher: DispatchAsync("Kitchen-01", document)
    Dispatcher->>Dispatcher: payloadBase64 = Convert.ToBase64String(document.GetBytes())
    Dispatcher->>Dispatcher: job = new PrintJobMessage(IdemKey, "Kitchen-01", DocName, payloadBase64, Now)
    Dispatcher->>Hub: Clients.Group("printer:KITCHEN-01").OnPrintJobReceived(job)
    Hub->>EdgeAgent: OnPrintJobReceived(job) [WebSocket Frame]
    EdgeAgent->>EdgeAgent: Convert.FromBase64String(job.PayloadBase64)
    EdgeAgent->>LocalPrinter: PrintAsync(localRawDoc)
    LocalPrinter-->>EdgeAgent: Result.Success(true)
    Dispatcher-->>Backend: Result.Success(true)
```

---

## 4. Hardware Diagnostic State Machine (ESC/POS Status)

```mermaid
stateDiagram-v2
    [*] --> Standby: Parser Initialization
    Standby --> SendingQuery: Transmit DLE EOT 1..4
    SendingQuery --> DecodingBytes: Receive 4 status bytes
    
    state DecodingBytes {
        [*] --> CheckCover
        CheckCover --> CoverOpen: Bit 2 in Byte 2 == 1
        CheckCover --> CoverClosed: Bit 2 in Byte 2 == 0
        
        CoverClosed --> CheckPaper
        CheckPaper --> PaperOut: Bit 5 in Byte 2 == 1 OR Bits 5,6 in Byte 4 == 1
        CheckPaper --> PaperNearEnd: Bits 2,3 in Byte 4 == 1
        CheckPaper --> PaperAdequate: Sensors clear
        
        PaperAdequate --> CheckCutter
        CheckCutter --> CutterJam: Bit 3 in Byte 3 == 1
        CheckCutter --> CutterOk: Bit 3 in Byte 3 == 0
    }
    
    DecodingBytes --> HealthyState: isOnline == true (No errors, cover closed, paper loaded)
    DecodingBytes --> ErrorState: hasError == true OR hasCutterError == true OR isPaperOut == true
    
    HealthyState --> ReadyToPrint: Allow print job transmission
    ErrorState --> HoldQueue: Pause queue and notify operator
```

---

## 5. Monochrome Graphics Processing Pipeline

```mermaid
flowchart LR
    INPUT["Input Image\n(24/32-bit BMP or RGB Buffer)"] --> LUMINANCE["Luminance Computation\n(0.299R + 0.587G + 0.114B)"]
    
    LUMINANCE --> DITHER{"Dithering\nAlgorithm"}
    
    DITHER -->|Threshold| THRESH["Direct Thresholding\n(Luminance < 128 = 1 Dot)"]
    DITHER -->|Floyd-Steinberg| DIFF["Error Diffusion Matrix\n(7/16, 3/16, 5/16, 1/16)"]
    
    THRESH --> PACK["Bit Packing\n(8 pixels per byte, MSB to LSB)"]
    DIFF --> PACK
    
    PACK --> PROTOCOL{"Target Protocol"}
    PROTOCOL -->|ESC/POS| ESCPOS_CMD["GS v 0 Command\n(Raster Bit Image)"]
    PROTOCOL -->|ZPL II| ZPL_CMD["^GF Command\n(Hexadecimal Graphic Field)"]
```

---

## 6. Layer Transitions & Separation of Concerns

1. **From Composition to Document**:
   Builders (`EscPosBuilder`, `ZplBuilder`) are mutable, single-threaded objects. Calling `.Build()` materializes an immutable `IPrintDocument` (`RawPrintDocument`, `ZplPrintDocument`) that is safe to share concurrently across threads or transmit to multiple endpoints.

2. **From Document to Transport**:
   The `IPrinterClient` abstraction consumes `IPrintDocument` and streams bytes asynchronously via `document.WriteToAsync(stream)` directly to physical socket or serial streams, avoiding memory re-allocation.

3. **From Transport to Resilience**:
   Low-level transport clients (`TcpPrinterClient`, `PooledTcpPrinterClient`, `SerialPrinterClient`) do not contain retry loops. Resilience is applied via the decorator pattern (`ResilientPrinterClient`), ensuring consistent retry behavior across any physical transport.
