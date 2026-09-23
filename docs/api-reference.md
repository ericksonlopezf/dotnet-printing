<!-- Copyright © Erickson Lopez. MIT License. -->
# API Reference — EricksonLopez.Printing

Comprehensive Microsoft Learn-style technical reference for all public contracts, classes, structs, records, extension methods, and error codes in the `EricksonLopez.Printing` library ecosystem.

---

## Namespace Index

1. [`EricksonLopez.Printing`](#1-ericksonlopezprinting)
   - [`IPrinterClient`](#iprinterclient)
   - [`IPrintDocument`](#iprintdocument)
   - [`RawPrintDocument`](#rawprintdocument)
   - [`TcpPrinterClientOptions`](#tcpprinterclientoptions)
   - [`TcpPrinterClient`](#tcpprinterclient)
   - [`PooledTcpPrinterClient`](#pooledtcpprinterclient)
   - [`ResilientPrinterOptions`](#resilientprinteroptions)
   - [`ResilientPrinterClient`](#resilientprinterclient)
   - [`ResilientPrintingExtensions`](#resilientprintingextensions)
   - [`PrintingServiceCollectionExtensions`](#printingservicecollectionextensions)
   - [`PrintingErrorCodes`](#printingerrorcodes)
   - [`PrintingEventIds`](#printingeventids)
2. [`EricksonLopez.Printing.EscPos`](#2-ericksonlopezprintingescpos)
   - [`EscPosAlignment`](#escposalignment)
   - [`EscPosQrErrorCorrection`](#escposqrerrorcorrection)
   - [`EscPosUnderline`](#escposunderline)
   - [`EscPosBuilder`](#escposbuilder)
3. [`EricksonLopez.Printing.EscPos.Imaging`](#3-ericksonlopezprintingescposimaging)
   - [`EscPosDitherAlgorithm`](#escposditheralgorithm)
   - [`EscPosImageBuilderExtensions`](#escposimagebuilderextensions)
   - [`MonochromeBitmapConverter`](#monochromebitmapconverter-escposimaging)
4. [`EricksonLopez.Printing.EscPos.Status`](#4-ericksonlopezprintingescposstatus)
   - [`EscPosPrinterStatus`](#escposprinterstatus)
   - [`EscPosStatusCommands`](#escposstatuscommands)
   - [`EscPosStatusParser`](#escposstatusparser)
5. [`EricksonLopez.Printing.Serial`](#5-ericksonlopezprintingserial)
   - [`SerialPrinterClientOptions`](#serialprinterclientoptions)
   - [`SerialPrinterClient`](#serialprinterclient)
   - [`PrintingSerialServiceCollectionExtensions`](#printingserialservicecollectionextensions)
6. [`EricksonLopez.Printing.SignalR`](#6-ericksonlopezprintingsignalr)
   - [`PrintJobMessage`](#printjobmessage)
   - [`IPrinterHubClient`](#iprinterhubclient)
   - [`PrinterHub`](#printerhub)
   - [`IHubPrinterDispatcher`](#ihubprinterdispatcher)
   - [`HubPrinterDispatcher`](#hubprinterdispatcher)
   - [`PrintingSignalRServiceCollectionExtensions`](#printingsignalrservicecollectionextensions)
   - [`SignalRPrintingEventIds`](#signalrprintingeventids)
7. [`EricksonLopez.Printing.Zpl`](#7-ericksonlopezprintingzpl)
   - [`ZplFont`](#zplfont)
   - [`ZplOrientation`](#zplorientation)
   - [`ZplPrintDocument`](#zplprintdocument)
   - [`ZplBuilder`](#zplbuilder)
8. [`EricksonLopez.Printing.Zpl.Imaging`](#8-ericksonlopezprintingzplimaging)
   - [`ZplDitherAlgorithm`](#zplditheralgorithm)
   - [`ZplGraphicFieldConverter`](#zplgraphicfieldconverter)
   - [`MonochromeBitmapConverter`](#monochromebitmapconverter-zplimaging)
   - [`ZplImageBuilderExtensions`](#zplimagebuilderextensions)

---

## 1. EricksonLopez.Printing

### IPrinterClient

Defines the fundamental contract for transmitting print documents to a destination (network socket, serial port, or cloud bridge).

```csharp
namespace EricksonLopez.Printing;

public interface IPrinterClient
{
    Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
}
```

#### PrintAsync

Asynchronously transmits a document payload to the target device.

```csharp
Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
```

- **Parameters:**
  - `document` (`IPrintDocument`): The immutable document payload to transmit. Cannot be null.
  - `cancellationToken` (`CancellationToken`): Token to cancel the I/O transmission or connection attempt.
- **Returns:** `Task<Result<bool>>`: Result containing `true` on successful transmission, or a typed domain `Error` on failure (e.g., `Printer.SocketError`, `Printer.Timeout`, `Printer.Canceled`).

---

### IPrintDocument

Defines a print document payload containing raw printer bytecode sequences.

```csharp
namespace EricksonLopez.Printing;

public interface IPrintDocument
{
    string DocumentName { get; }
    byte[] GetBytes();
    bool IsEmpty => false;
    bool IsIdempotent => false;
    ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default);
    ReadOnlyMemory<byte> GetMemory() => GetBytes();
    string? IdempotencyKey => null;
}
```

- **Properties:**
  - `DocumentName` (`string`): Human-readable identifier for telemetry and diagnostics.
  - `IsEmpty` (`bool`): Indicates if the document contains zero printable commands.
  - `IsIdempotent` (`bool`): Indicates whether the document is safe to retry if a mid-stream transmission error occurs.
  - `IdempotencyKey` (`string?`): Optional unique identifier used for deduplication across retries.
- **Methods:**
  - `GetBytes()` (`byte[]`): Returns document payload as a contiguous byte array.
  - `GetMemory()` (`ReadOnlyMemory<byte>`): Exposes document memory directly without defensive copying.
  - `WriteToAsync(Stream, CancellationToken)` (`ValueTask`): Streams commands directly to network or serial streams.

---

### RawPrintDocument

In-memory implementation of `IPrintDocument` backed by a byte array with idempotency tracking.

```csharp
namespace EricksonLopez.Printing;

public sealed class RawPrintDocument : IPrintDocument
{
    public RawPrintDocument(byte[] bytes, string documentName = "PrintJob", string? idempotencyKey = null, bool isIdempotent = false);
    
    public string DocumentName { get; }
    public string? IdempotencyKey { get; }
    public bool IsEmpty => _bytes.Length == 0;
    public bool IsIdempotent { get; }
    public byte[] GetBytes();
    public ReadOnlyMemory<byte> GetMemory();
    public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default);
}
```

---

### TcpPrinterClientOptions

Configuration options for connecting to network printers over raw TCP sockets (typically port 9100).

```csharp
namespace EricksonLopez.Printing;

public sealed class TcpPrinterClientOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9100;
    public int TimeoutMs { get; set; } = 5000;
    public bool NoDelay { get; set; } = true;
    public int LingerSeconds { get; set; } = 0;
}
```

- `Host` (`string`): IP address or hostname of the printer.
- `Port` (`int`): TCP raw socket port (standard: 9100).
- `TimeoutMs` (`int`): Connection and write timeout in milliseconds.
- `NoDelay` (`bool`): Enables `TCP_NODELAY` (disables Nagle algorithm) for minimal latency.
- `LingerSeconds` (`int`): Sets `SO_LINGER` to instantly release sockets and prevent `TIME_WAIT` buildup.

---

### TcpPrinterClient

Ephemeral TCP printer client establishing a new socket connection per print job.

```csharp
namespace EricksonLopez.Printing;

public sealed class TcpPrinterClient : IPrinterClient
{
    public TcpPrinterClient(TcpPrinterClientOptions options);
    public TcpPrinterClient(TcpPrinterClientOptions options, ILogger<TcpPrinterClient>? logger = null);

    public Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
}
```

---

### PooledTcpPrinterClient

High-throughput persistent TCP client maintaining a single open socket across jobs with `SemaphoreSlim(1, 1)` serialization.

```csharp
namespace EricksonLopez.Printing;

public sealed class PooledTcpPrinterClient : IPrinterClient, IAsyncDisposable, IDisposable
{
    public PooledTcpPrinterClient(TcpPrinterClientOptions options);
    public PooledTcpPrinterClient(TcpPrinterClientOptions options, ILogger<PooledTcpPrinterClient>? logger = null);

    public Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
    public void Dispose();
}
```

---

### ResilientPrinterOptions

Configuration options for the exponential backoff retry decorator.

```csharp
namespace EricksonLopez.Printing;

public sealed class ResilientPrinterOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool RetryOnTransmissionError { get; set; } = false;
}
```

- `RetryOnTransmissionError` (`bool`): Invariant controlling physical duplicate ticket protection. When false, mid-stream connection drops abort immediately for non-idempotent print documents.

---

### ResilientPrinterClient

Resilient decorator implementing exponential backoff retries with physical idempotency protection.

```csharp
namespace EricksonLopez.Printing;

public sealed class ResilientPrinterClient : IPrinterClient
{
    public ResilientPrinterClient(
        IPrinterClient innerClient,
        ResilientPrinterOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<ResilientPrinterClient>? logger = null);

    public Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
}
```

---

### ResilientPrintingExtensions

Fluent extension method to wrap any `IPrinterClient` with a `ResilientPrinterClient`.

```csharp
namespace EricksonLopez.Printing;

public static class ResilientPrintingExtensions
{
    public static IPrinterClient WithRetry(
        this IPrinterClient client,
        Action<ResilientPrinterOptions>? configure = null,
        TimeProvider? timeProvider = null,
        ILogger<ResilientPrinterClient>? logger = null);
}
```

---

### PrintingServiceCollectionExtensions

Dependency injection extensions for Microsoft.Extensions.DependencyInjection.

```csharp
namespace EricksonLopez.Printing;

public static class PrintingServiceCollectionExtensions
{
    public static IServiceCollection AddTcpPrinter(this IServiceCollection services, Action<TcpPrinterClientOptions> configure);
    public static IServiceCollection AddKeyedTcpPrinter(this IServiceCollection services, object? serviceKey, Action<TcpPrinterClientOptions> configure);
    public static IServiceCollection AddPooledTcpPrinter(this IServiceCollection services, Action<TcpPrinterClientOptions> configure);
    public static IServiceCollection AddKeyedPooledTcpPrinter(this IServiceCollection services, object? serviceKey, Action<TcpPrinterClientOptions> configure);
}
```

---

### PrintingErrorCodes

Standard domain error codes returned by the Result Pattern pipeline.

```csharp
namespace EricksonLopez.Printing;

public static class PrintingErrorCodes
{
    public const string SocketError = "Printer.SocketError";
    public const string TransmissionError = "Printer.TransmissionError";
    public const string Timeout = "Printer.Timeout";
    public const string Canceled = "Printer.Canceled";
}
```

---

### PrintingEventIds

Standard `EventId` constants for structured logging and OpenTelemetry integration.

```csharp
namespace EricksonLopez.Printing;

public static class PrintingEventIds
{
    public static readonly EventId TcpPrintStarted = new(1001, nameof(TcpPrintStarted));
    public static readonly EventId TcpPrintSuccess = new(1002, nameof(TcpPrintSuccess));
    public static readonly EventId TcpPrintFailed = new(1003, nameof(TcpPrintFailed));
    public static readonly EventId SerialPrintStarted = new(1004, nameof(SerialPrintStarted));
    public static readonly EventId SerialPrintSuccess = new(1005, nameof(SerialPrintSuccess));
    public static readonly EventId SerialPrintFailed = new(1006, nameof(SerialPrintFailed));
    public static readonly EventId RetryAttempt = new(1010, nameof(RetryAttempt));
    public static readonly EventId RetryExhausted = new(1011, nameof(RetryExhausted));
}
```

---

## 2. EricksonLopez.Printing.EscPos

### EscPosAlignment

Specifies text and element justification in ESC/POS receipts (`ESC a n`).

```csharp
namespace EricksonLopez.Printing.EscPos;

public enum EscPosAlignment : byte
{
    Left = 0,
    Center = 1,
    Right = 2
}
```

---

### EscPosQrErrorCorrection

Error correction levels for ESC/POS Model 2 QR codes.

```csharp
namespace EricksonLopez.Printing.EscPos;

public enum EscPosQrErrorCorrection : byte
{
    L = 48,
    M = 49,
    Q = 50,
    H = 51
}
```

---

### EscPosUnderline

Specifies underline text styling (`ESC - n`).

```csharp
namespace EricksonLopez.Printing.EscPos;

public enum EscPosUnderline : byte
{
    None = 0,
    SingleDot = 1,
    DoubleDot = 2
}
```

---

### EscPosBuilder

Fluent, single-threaded compiler for standard Epson ESC/POS receipt commands. Implements `IDisposable`.

```csharp
namespace EricksonLopez.Printing.EscPos;

public sealed class EscPosBuilder : IDisposable
{
    public EscPosBuilder(Encoding? encoding = null, bool safeMode = true);

    public EscPosBuilder Initialize();
    public EscPosBuilder Raw(ReadOnlySpan<byte> rawBytes);
    public EscPosBuilder Raw(byte[] rawBytes);
    public EscPosBuilder Align(EscPosAlignment alignment);
    public EscPosBuilder Bold(bool enable = true);
    public EscPosBuilder DoubleSize(bool doubleWidth = true, bool doubleHeight = true);
    public EscPosBuilder Underline(EscPosUnderline underline = EscPosUnderline.SingleDot);
    public EscPosBuilder Underline(bool enable, bool doubleThickness = false);
    public EscPosBuilder FontSize(int widthMultiplier = 1, int heightMultiplier = 1);
    public EscPosBuilder Text(string text);
    public EscPosBuilder Line(string text = "");
    public EscPosBuilder Divider(int totalWidth = 32, char dividerChar = '-');
    public EscPosBuilder TableRow(string left, string right, int totalWidth = 32);
    public EscPosBuilder BarcodeCode128(string data, int height = 64, bool showHri = true);
    public EscPosBuilder BarcodeCode39(string data, int height = 64, int width = 2, bool showHri = true);
    public EscPosBuilder BarcodeEan13(string data, int height = 64, int width = 2, bool showHri = true);
    public EscPosBuilder Feed(int lines = 1);
    public EscPosBuilder Cut(bool partial = false);
    public EscPosBuilder OpenCashDrawer();
    public EscPosBuilder QrCode(string data, int moduleSize = 6, EscPosQrErrorCorrection errorCorrection = EscPosQrErrorCorrection.M);

    public IPrintDocument Build(string documentName = "EscPosDocument", string? idempotencyKey = null);
    public void Dispose();
}
```

---

## 3. EricksonLopez.Printing.EscPos.Imaging

### EscPosDitherAlgorithm

Monochrome dithering algorithms for thermal image rasterization.

```csharp
namespace EricksonLopez.Printing.EscPos.Imaging;

public enum EscPosDitherAlgorithm
{
    Threshold,
    FloydSteinberg
}
```

---

### EscPosImageBuilderExtensions

Extension methods adding monochrome image rendering to `EscPosBuilder`.

```csharp
namespace EricksonLopez.Printing.EscPos.Imaging;

public static class EscPosImageBuilderExtensions
{
    public static EscPosBuilder Image(this EscPosBuilder builder, byte[] bmpBytes, EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg, byte scale = 0, int maxAllowedDimension = MonochromeBitmapConverter.MaxDimension);
    public static EscPosBuilder Image(this EscPosBuilder builder, int width, int height, ReadOnlySpan<byte> pixelData, int bytesPerPixel = 3, EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg, byte scale = 0, int maxAllowedDimension = MonochromeBitmapConverter.MaxDimension);
    public static EscPosBuilder RasterImage(this EscPosBuilder builder, int width, int height, ReadOnlySpan<byte> packedRaster, byte scale = 0);
}
```

---

### MonochromeBitmapConverter (EscPos.Imaging)

Pure managed C# image converter producing 1-bit packed raster for ESC/POS (`GS v 0`).

```csharp
namespace EricksonLopez.Printing.EscPos.Imaging;

public static class MonochromeBitmapConverter
{
    public const int MaxDimension = 8192;

    public static byte[] ConvertBmpToPacked1Bit(ReadOnlySpan<byte> bmpData, EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg, int maxDimension = MaxDimension);
    public static byte[] ConvertRgbToPacked1Bit(ReadOnlySpan<byte> rgbBytes, int width, int height, EscPosDitherAlgorithm algorithm = EscPosDitherAlgorithm.FloydSteinberg, int maxDimension = MaxDimension);
    public static byte[] BuildEscPosRasterCommand(ReadOnlySpan<byte> packedRaster, int width, int height, int scale = 0);
}
```

---

## 4. EricksonLopez.Printing.EscPos.Status

### EscPosPrinterStatus

Immutable record exposing decoded hardware sensor states.

```csharp
namespace EricksonLopez.Printing.EscPos.Status;

public sealed record EscPosPrinterStatus(
    bool IsOnline,
    bool IsCoverOpen,
    bool IsPaperOut,
    bool IsPaperNearEnd,
    bool IsDrawerOpen,
    bool HasError,
    bool HasCutterError);
```

---

### EscPosStatusCommands

Pre-allocated command sequences for real-time `DLE EOT n` status polling.

```csharp
namespace EricksonLopez.Printing.EscPos.Status;

public static class EscPosStatusCommands
{
    public static ReadOnlyMemory<byte> QueryPrinterStatus { get; } // DLE EOT 1
    public static ReadOnlyMemory<byte> QueryOfflineCause { get; }  // DLE EOT 2
    public static ReadOnlyMemory<byte> QueryErrorStatus { get; }   // DLE EOT 3
    public static ReadOnlyMemory<byte> QueryPaperSensor { get; }   // DLE EOT 4
}
```

---

### EscPosStatusParser

Zero-allocation status byte parser for ESC/POS firmware telemetry.

```csharp
namespace EricksonLopez.Printing.EscPos.Status;

public static class EscPosStatusParser
{
    public static EscPosPrinterStatus Parse(byte b1, byte b2, byte b3, byte b4);
    public static void ParsePrinterStatusByte(byte b1, ref bool isDrawerOpen, ref bool isOnline);
    public static void ParseOfflineCauseByte(byte b2, ref bool isCoverOpen, ref bool isPaperOut, ref bool hasError);
    public static void ParseErrorStatusByte(byte b3, ref bool hasCutterError, ref bool hasUnrecoverableError);
    public static void ParsePaperSensorByte(byte b4, ref bool isPaperNearEnd, ref bool isPaperOut);
}
```

---

## 5. EricksonLopez.Printing.Serial

### SerialPrinterClientOptions

Options for connecting to physical RS-232 and Virtual COM ports.

```csharp
namespace EricksonLopez.Printing;

public sealed class SerialPrinterClientOptions
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public Parity Parity { get; set; } = Parity.None;
    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;
    public int WriteTimeoutMs { get; set; } = 5000;
    public int ReadTimeoutMs { get; set; } = 5000;
}
```

---

### SerialPrinterClient

Serial port printer client wrapping `System.IO.Ports.SerialPort` with `SemaphoreSlim(1, 1)` serialization. Implements `IDisposable`.

```csharp
namespace EricksonLopez.Printing.Serial;

public sealed class SerialPrinterClient : IPrinterClient, IDisposable
{
    public SerialPrinterClient(SerialPrinterClientOptions options);
    public SerialPrinterClient(SerialPrinterClientOptions options, ILogger<SerialPrinterClient>? logger = null);

    public Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
    public void Dispose();
}
```

---

### PrintingSerialServiceCollectionExtensions

DI extensions for registering serial printer clients.

```csharp
namespace EricksonLopez.Printing;

public static class PrintingSerialServiceCollectionExtensions
{
    public static IServiceCollection AddSerialPrinter(this IServiceCollection services, Action<SerialPrinterClientOptions> configure);
    public static IServiceCollection AddKeyedSerialPrinter(this IServiceCollection services, object? serviceKey, Action<SerialPrinterClientOptions> configure);
}
```

---

## 6. EricksonLopez.Printing.SignalR

### PrintJobMessage

Transport DTO for broadcasting print jobs across SignalR WebSocket connections.

```csharp
namespace EricksonLopez.Printing.SignalR;

public sealed record PrintJobMessage(
    string JobId,
    string PrinterName,
    string DocumentName,
    string PayloadBase64,
    DateTimeOffset DispatchedAt);
```

---

### IPrinterHubClient

Strongly-typed client interface implemented by distributed edge printer daemons.

```csharp
namespace EricksonLopez.Printing.SignalR;

public interface IPrinterHubClient
{
    Task OnPrintJobReceived(PrintJobMessage job);
}
```

---

### PrinterHub

ASP.NET Core SignalR hub coordinating branch printer registrations and group routing.

```csharp
namespace EricksonLopez.Printing.SignalR;

public sealed class PrinterHub : Hub<IPrinterHubClient>
{
    public Task RegisterPrinter(string printerName);
    public Task RegisterPrinterAsync(string printerName);
    public Task UnregisterPrinter(string printerName);
    public Task UnregisterPrinterAsync(string printerName);
}
```

---

### IHubPrinterDispatcher

High-level dispatcher service used by SaaS backend code to dispatch jobs to edge printers.

```csharp
namespace EricksonLopez.Printing.SignalR;

public interface IHubPrinterDispatcher
{
    Task<Result<bool>> DispatchAsync(string printerName, IPrintDocument document, CancellationToken cancellationToken = default);
}
```

---

### HubPrinterDispatcher

Implementation of `IHubPrinterDispatcher` encoding documents into `PrintJobMessage` with telemetry.

```csharp
namespace EricksonLopez.Printing.SignalR;

public sealed class HubPrinterDispatcher : IHubPrinterDispatcher
{
    public HubPrinterDispatcher(IHubContext<PrinterHub, IPrinterHubClient> hubContext, TimeProvider? timeProvider = null, ILogger<HubPrinterDispatcher>? logger = null);

    public Task<Result<bool>> DispatchAsync(string printerName, IPrintDocument document, CancellationToken cancellationToken = default);
}
```

---

### PrintingSignalRServiceCollectionExtensions

DI extension registering SignalR printing services.

```csharp
namespace EricksonLopez.Printing.SignalR;

public static class PrintingSignalRServiceCollectionExtensions
{
    public static IServiceCollection AddPrintingSignalR(this IServiceCollection services);
}
```

---

### SignalRPrintingEventIds

Event IDs for SignalR cloud dispatch telemetry.

```csharp
namespace EricksonLopez.Printing.SignalR;

public static class SignalRPrintingEventIds
{
    public static readonly EventId DispatchStarted = new(2001, nameof(DispatchStarted));
    public static readonly EventId DispatchSuccess = new(2002, nameof(DispatchSuccess));
    public static readonly EventId DispatchFailed = new(2003, nameof(DispatchFailed));
}
```

---

## 7. EricksonLopez.Printing.Zpl

### ZplFont

Strongly-typed enum of native Zebra printer fonts.

```csharp
namespace EricksonLopez.Printing.Zpl;

public enum ZplFont
{
    Font0 = '0',
    FontA = 'A',
    FontB = 'B',
    FontC = 'C',
    FontD = 'D',
    FontE = 'E',
    FontF = 'F',
    FontG = 'G',
    FontH = 'H'
}
```

---

### ZplOrientation

Rotation enum for Zebra text and barcode fields.

```csharp
namespace EricksonLopez.Printing.Zpl;

public enum ZplOrientation
{
    Normal = 'N',
    Rotated90 = 'R',
    Inverted180 = 'I',
    BottomUp270 = 'B'
}
```

---

### ZplPrintDocument

Stream-based implementation of `IPrintDocument` for Zebra ZPL II payloads.

```csharp
namespace EricksonLopez.Printing.Zpl;

public sealed class ZplPrintDocument : IPrintDocument
{
    public ZplPrintDocument(string documentName, string zplContent, string? idempotencyKey = null, bool isIdempotent = false);

    public string DocumentName { get; }
    public string? IdempotencyKey { get; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(_zplContent);
    public bool IsIdempotent { get; }
    public byte[] GetBytes();
    public ReadOnlyMemory<byte> GetMemory();
    public ValueTask WriteToAsync(Stream destination, CancellationToken cancellationToken = default);
}
```

---

### ZplBuilder

Fluent compiler for Zebra Programming Language (ZPL II) industrial labels.

```csharp
namespace EricksonLopez.Printing.Zpl;

public sealed class ZplBuilder
{
    public ZplBuilder(bool safeMode = false);

    public ZplBuilder Raw(string rawZpl);
    public ZplBuilder Text(int x, int y, string text, int height = 30, int width = 30, char font = '0', ZplOrientation orientation = ZplOrientation.Normal);
    public ZplBuilder Text(int x, int y, string text, ZplFont font, int height = 30, int width = 30, ZplOrientation orientation = ZplOrientation.Normal);
    public ZplBuilder BarcodeCode128(int x, int y, string data, int height = 60, bool showText = true, ZplOrientation orientation = ZplOrientation.Normal);
    public ZplBuilder BarcodeCode39(int x, int y, string data, int height = 60, int width = 2, bool showText = true, ZplOrientation orientation = ZplOrientation.Normal);
    public ZplBuilder BarcodeEan13(int x, int y, string data, int height = 60, int width = 2, bool showText = true, ZplOrientation orientation = ZplOrientation.Normal);
    public ZplBuilder QrCode(int x, int y, string data, int magnification = 5, ZplOrientation orientation = ZplOrientation.Normal);
    public ZplBuilder Box(int x, int y, int width, int height, int borderThickness = 1, bool invert = false, int cornerRounding = 0);
    public ZplBuilder Circle(int x, int y, int diameter, int borderThickness = 1, bool invert = false);
    public ZplBuilder Ellipse(int x, int y, int width, int height, int borderThickness = 1, bool invert = false);
    public ZplBuilder DiagonalLine(int x, int y, int width, int height, int borderThickness = 1, bool invert = false, bool rightLeaning = false);

    public IPrintDocument Build(string documentName = "ZplDocument", string? idempotencyKey = null);
}
```

---

## 8. EricksonLopez.Printing.Zpl.Imaging

### ZplDitherAlgorithm

Monochrome dithering algorithms for Zebra labels.

```csharp
namespace EricksonLopez.Printing.Zpl.Imaging;

public enum ZplDitherAlgorithm
{
    Threshold,
    FloydSteinberg
}
```

---

### ZplGraphicFieldConverter

Converts packed 1-bit monochrome raster into hexadecimal ZPL II `^GF` commands.

```csharp
namespace EricksonLopez.Printing.Zpl.Imaging;

public static class ZplGraphicFieldConverter
{
    public static string BuildGraphicFieldCommand(ReadOnlySpan<byte> packedRaster, int width, int height);
}
```

---

### MonochromeBitmapConverter (Zpl.Imaging)

Pure managed C# image converter generating packed 1-bit raster for ZPL labels.

```csharp
namespace EricksonLopez.Printing.Zpl.Imaging;

public static class MonochromeBitmapConverter
{
    public const int MaxDimension = 8192;

    public static byte[] ConvertBmpToPacked1Bit(ReadOnlySpan<byte> bmpData, ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg, int maxDimension = MaxDimension);
    public static byte[] ConvertRgbToPacked1Bit(ReadOnlySpan<byte> rgbBytes, int width, int height, ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg, int maxDimension = MaxDimension);
}
```

---

### ZplImageBuilderExtensions

Extension methods adding image and graphic field generation to `ZplBuilder`.

```csharp
namespace EricksonLopez.Printing.Zpl.Imaging;

public static class ZplImageBuilderExtensions
{
    public static ZplBuilder GraphicField(this ZplBuilder builder, int x, int y, ReadOnlySpan<byte> packedRaster, int width, int height);
    public static ZplBuilder Image(this ZplBuilder builder, int x, int y, byte[] bmpBytes, ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg);
    public static ZplBuilder Image(this ZplBuilder builder, int x, int y, ReadOnlySpan<byte> rgbBytes, int width, int height, ZplDitherAlgorithm algorithm = ZplDitherAlgorithm.FloydSteinberg);
}
```
