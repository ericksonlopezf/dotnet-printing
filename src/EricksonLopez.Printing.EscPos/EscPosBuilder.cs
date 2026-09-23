// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos;

using System;
using System.IO;
using System.Text;

using Microsoft.IO;

/// <summary>
/// Provides a fluent builder for composing Epson ESC/POS receipt printer command sequences.
/// </summary>
/// <remarks>
/// <para>Builder instances are not thread-safe and must not be shared across concurrent threads.
/// Instances are designed for single-threaded sequential composition of a single print job.
/// The output <see cref="IPrintDocument"/> produced by <see cref="Build"/> is immutable and thread-safe.
/// </para>
/// <para>Releases internal memory streams when disposed. Always use within a <see langword="using"/> block
/// or explicitly call <see cref="Dispose"/>.</para>
/// </remarks>
public sealed class EscPosBuilder : IDisposable
{
    private static readonly RecyclableMemoryStreamManager _streamManager = new();
    private readonly MemoryStream _buffer;
    private readonly Encoding _encoding;
    private readonly bool _safeMode;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EscPosBuilder"/> class with the specified encoding and safety mode.
    /// </summary>
    /// <param name="encoding">The character encoding to use for text commands. Defaults to UTF-8.</param>
    /// <param name="safeMode"><see langword="true"/> to sanitize control characters in text; otherwise, <see langword="false"/>.</param>
    public EscPosBuilder(Encoding? encoding = null, bool safeMode = true)
    {
        _buffer = _streamManager.GetStream();
        _encoding = encoding ?? Encoding.UTF8;
        _safeMode = safeMode;
    }

    /// <summary>
    /// Resets printer hardware to its default initial state.
    /// </summary>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1B);
        _buffer.WriteByte(0x40);
        return this;
    }

    /// <summary>
    /// Appends raw binary printer command sequences directly to the buffer.
    /// </summary>
    /// <param name="rawBytes">The raw byte span to append.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    /// <exception cref="NotSupportedException">Safe mode is enabled</exception>
    public EscPosBuilder Raw(ReadOnlySpan<byte> rawBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_safeMode)
        {
            throw new NotSupportedException("Raw printing is not supported in safe mode.");
        }
        if (!rawBytes.IsEmpty)
        {
            _buffer.Write(rawBytes);
        }
        return this;
    }

    /// <summary>
    /// Appends raw binary printer command sequences directly to the buffer.
    /// </summary>
    /// <param name="rawBytes">The raw byte array to append.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rawBytes"/> is <see langword="null"/></exception>
    /// <exception cref="NotSupportedException">Safe mode is enabled</exception>
    public EscPosBuilder Raw(byte[] rawBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(rawBytes);
        return Raw(rawBytes.AsSpan());
    }

    /// <summary>
    /// Sets the horizontal text and graphics alignment.
    /// </summary>
    /// <param name="alignment">The alignment mode to apply.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Align(EscPosAlignment alignment)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1B);
        _buffer.WriteByte(0x61);
        _buffer.WriteByte((byte)alignment);
        return this;
    }

    /// <summary>
    /// Configures emphasized or bold text mode.
    /// </summary>
    /// <param name="enable"><see langword="true"/> to enable bold text; <see langword="false"/> to disable.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Bold(bool enable = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1B);
        _buffer.WriteByte(0x45);
        _buffer.WriteByte((byte)(enable ? 1 : 0));
        return this;
    }

    /// <summary>
    /// Configures double-width and double-height font magnification.
    /// </summary>
    /// <param name="doubleWidth"><see langword="true"/> to enable double-width characters; otherwise, <see langword="false"/>.</param>
    /// <param name="doubleHeight"><see langword="true"/> to enable double-height characters; otherwise, <see langword="false"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder DoubleSize(bool doubleWidth = true, bool doubleHeight = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x21);
        byte n = (byte)((doubleWidth ? 0x20 : 0) | (doubleHeight ? 0x01 : 0));
        _buffer.WriteByte(n);
        return this;
    }

    /// <summary>
    /// Configures the underline text mode.
    /// </summary>
    /// <param name="underline">The underline style to apply.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Underline(EscPosUnderline underline = EscPosUnderline.SingleDot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1B);
        _buffer.WriteByte(0x2D);
        _buffer.WriteByte((byte)underline);
        return this;
    }

    /// <summary>
    /// Configures the underline text mode with optional double thickness.
    /// </summary>
    /// <param name="enable"><see langword="true"/> to enable underline; <see langword="false"/> to disable.</param>
    /// <param name="doubleThickness"><see langword="true"/> to use double-dot thickness; otherwise, <see langword="false"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    public EscPosBuilder Underline(bool enable, bool doubleThickness = false)
    {
        if (!enable)
        {
            return Underline(EscPosUnderline.None);
        }

        return Underline(doubleThickness ? EscPosUnderline.DoubleDot : EscPosUnderline.SingleDot);
    }

    /// <summary>
    /// Sets character font scale multipliers from 1 to 8 times normal width and height.
    /// </summary>
    /// <param name="widthMultiplier">The horizontal scaling multiplier (1 to 8).</param>
    /// <param name="heightMultiplier">The vertical scaling multiplier (1 to 8).</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder FontSize(int widthMultiplier = 1, int heightMultiplier = 1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var w = Math.Clamp(widthMultiplier, 1, 8) - 1;
        var h = Math.Clamp(heightMultiplier, 1, 8) - 1;
        byte n = (byte)((w << 4) | h);

        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x21);
        _buffer.WriteByte(n);
        return this;
    }

    /// <summary>
    /// Appends text to the print buffer without appending a line feed.
    /// </summary>
    /// <param name="text">The string content to append.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Text(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(text))
        {
            return this;
        }

        if (_safeMode)
        {
            // Basic sanitization of ESC/POS control characters, including DLE (Real-Time commands)
            text = text.Replace("\x1B", "").Replace("\x1C", "").Replace("\x1D", "").Replace("\x10", "");
        }

        var encoder = _encoding.GetEncoder();
        var charSpan = text.AsSpan();

        // Chunk processing to prevent LOH allocation for huge strings
        const int ChunkSize = 4096;
        var maxByteCount = _encoding.GetMaxByteCount(ChunkSize);
        // Stryker disable all : ArrayPool buffer chunking optimization
        var byteBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            int charsRead = 0;
            while (charsRead < charSpan.Length)
            {
                int remainingChars = charSpan.Length - charsRead;
                int currentChunkSize = Math.Min(ChunkSize, remainingChars);
                bool flush = (charsRead + currentChunkSize) >= charSpan.Length;

                encoder.Convert(charSpan.Slice(charsRead, currentChunkSize), byteBuffer, flush, out int charsUsed, out int bytesUsed, out _);
                _buffer.Write(byteBuffer, 0, bytesUsed);
                charsRead += charsUsed;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(byteBuffer);
        }
        // Stryker restore all
        return this;
    }

    /// <summary>
    /// Appends a line of text followed by a line feed.
    /// </summary>
    /// <param name="text">The string content to append before the line feed. Defaults to empty string.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Line(string text = "")
    {
        Text(text);
        _buffer.WriteByte(0x0A);
        return this;
    }

    /// <summary>
    /// Appends a two-column row with left- and right-aligned text separated by a filler character.
    /// </summary>
    /// <param name="left">The left column text.</param>
    /// <param name="right">The right column text.</param>
    /// <param name="totalWidth">The total line width in characters. Defaults to 42.</param>
    /// <param name="fillChar">The filler character between left and right columns. Defaults to space.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    public EscPosBuilder TableRow(string left, string right, int totalWidth = 42, char fillChar = ' ')
    {
        left ??= string.Empty;
        right ??= string.Empty;

        totalWidth = Math.Clamp(totalWidth, 1, 4096);

        var available = totalWidth - left.Length - right.Length;
        if (available <= 0)
        {
            Line(left);
            Line(right.PadLeft(Math.Max(totalWidth, right.Length)));
            return this;
        }

        var line = left + new string(fillChar, available) + right;
        return Line(line);
    }

    /// <summary>
    /// Appends a full-width divider line using the specified character.
    /// </summary>
    /// <param name="width">The total line width in characters. Defaults to 42.</param>
    /// <param name="character">The divider character to repeat. Defaults to '-'.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    public EscPosBuilder Divider(int width = 42, char character = '-')
    {
        if (width <= 0)
        {
            return this;
        }

        width = Math.Min(width, 4096);
        return Line(new string(character, width));
    }

    /// <summary>
    /// Appends a one-dimensional Code 128 barcode.
    /// </summary>
    /// <param name="data">The barcode alphanumeric data.</param>
    /// <param name="height">The barcode height in dots. Defaults to 64.</param>
    /// <param name="showHri"><see langword="true"/> to print Human Readable Interpretation (HRI) text below the barcode; otherwise, <see langword="false"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> is empty, consists only of white-space characters, or exceeds 255 bytes</exception>
    public EscPosBuilder BarcodeCode128(string data, int height = 64, bool showHri = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        WriteBarcodePreamble(height, showHri);

        // Print Code 128 (GS k 73 len bytes...)
        var dataBytes = _encoding.GetBytes(data);
        if (dataBytes.Length > 255)
        {
            throw new ArgumentException($"Code 128 barcode data length ({dataBytes.Length} bytes) exceeds maximum allowed 255 bytes.", nameof(data));
        }

        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x6B);
        _buffer.WriteByte(0x49); // Code 128 (73 in decimal = 0x49)
        _buffer.WriteByte((byte)dataBytes.Length);
        _buffer.Write(dataBytes, 0, dataBytes.Length);

        return this;
    }

    /// <summary>
    /// Appends a one-dimensional Code 39 barcode.
    /// </summary>
    /// <param name="data">The barcode alphanumeric data.</param>
    /// <param name="height">The barcode height in dots. Defaults to 64.</param>
    /// <param name="width">The barcode module width (2 to 6). Defaults to 2.</param>
    /// <param name="showHri"><see langword="true"/> to print Human Readable Interpretation (HRI) text below the barcode; otherwise, <see langword="false"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> is empty, consists only of white-space characters, or exceeds 255 bytes</exception>
    public EscPosBuilder BarcodeCode39(string data, int height = 64, int width = 2, bool showHri = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        WriteBarcodePreamble(height, showHri, width);

        // Print Code 39 (GS k 69 len bytes...)
        var dataBytes = _encoding.GetBytes(data);
        if (dataBytes.Length > 255)
        {
            throw new ArgumentException($"Code 39 barcode data length ({dataBytes.Length} bytes) exceeds maximum allowed 255 bytes.", nameof(data));
        }

        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x6B);
        _buffer.WriteByte(0x45); // 69 in decimal = 0x45 (Code 39 Function B)
        _buffer.WriteByte((byte)dataBytes.Length);
        _buffer.Write(dataBytes, 0, dataBytes.Length);

        return this;
    }

    /// <summary>
    /// Appends a one-dimensional EAN-13 barcode.
    /// </summary>
    /// <param name="data">The 12 or 13 numeric digits. If 12 digits are provided, the check digit is computed automatically.</param>
    /// <param name="height">The barcode height in dots. Defaults to 64.</param>
    /// <param name="width">The barcode module width (2 to 6). Defaults to 2.</param>
    /// <param name="showHri"><see langword="true"/> to print Human Readable Interpretation (HRI) text below the barcode; otherwise, <see langword="false"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> does not contain 12 or 13 numeric digits, or contains an invalid check digit</exception>
    public EscPosBuilder BarcodeEan13(string data, int height = 64, int width = 2, bool showHri = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        var sanitized = data.Trim();
        if (!IsNumeric(sanitized) || (sanitized.Length != 12 && sanitized.Length != 13))
        {
            throw new ArgumentException("EAN-13 barcode data must contain exactly 12 or 13 numeric digits.", nameof(data));
        }

        if (sanitized.Length == 12)
        {
            sanitized += CalculateEan13CheckDigit(sanitized);
        }
        else
        {
            var expectedChecksum = CalculateEan13CheckDigit(sanitized[..12]);
            if (sanitized[12] != expectedChecksum)
            {
                throw new ArgumentException($"Invalid EAN-13 check digit '{sanitized[12]}'. Expected '{expectedChecksum}'.", nameof(data));
            }
        }

        WriteBarcodePreamble(height, showHri, width);

        // Print EAN-13 (GS k 67 len bytes...)
        var dataBytes = _encoding.GetBytes(sanitized);
        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x6B);
        _buffer.WriteByte(0x43); // 67 in decimal = 0x43 (EAN-13 Function B)
        _buffer.WriteByte((byte)dataBytes.Length);
        _buffer.Write(dataBytes, 0, dataBytes.Length);

        return this;
    }

    private void WriteBarcodePreamble(int height, bool showHri, int? width = null)
    {
        if (width.HasValue)
        {
            _buffer.WriteByte(0x1D);
            _buffer.WriteByte(0x77);
            _buffer.WriteByte((byte)Math.Clamp(width.Value, 2, 6));
        }

        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x48);
        _buffer.WriteByte((byte)(showHri ? 2 : 0));

        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x68);
        _buffer.WriteByte((byte)Math.Clamp(height, 1, 255));
    }

    private static bool IsNumeric(string str) => str.All(char.IsAsciiDigit);

    private static char CalculateEan13CheckDigit(string digits12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = digits12[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }
        var remainder = sum % 10;
        var check = (10 - remainder) % 10;
        return (char)('0' + check);
    }

    /// <summary>
    /// Feeds paper by the specified number of lines.
    /// </summary>
    /// <param name="lines">The number of lines to feed. Defaults to 1.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Feed(int lines = 1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1B);
        _buffer.WriteByte(0x64);
        _buffer.WriteByte((byte)Math.Clamp(lines, 1, 255));
        return this;
    }

    /// <summary>
    /// Cuts the receipt paper.
    /// </summary>
    /// <param name="partial"><see langword="true"/> to perform a partial cut leaving a connecting tab; <see langword="false"/> for a full cut.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder Cut(bool partial = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1D);
        _buffer.WriteByte(0x56);
        _buffer.WriteByte((byte)(partial ? 0x01 : 0x00));
        return this;
    }

    /// <summary>
    /// Generates the electrical pulse to trigger a connected cash drawer.
    /// </summary>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    public EscPosBuilder OpenCashDrawer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.WriteByte(0x1B);
        _buffer.WriteByte(0x70);
        _buffer.WriteByte(0x00);
        _buffer.WriteByte(0x19);
        _buffer.WriteByte(0xFA);
        return this;
    }

    /// <summary>
    /// Generates a standard ESC/POS two-dimensional QR code.
    /// </summary>
    /// <param name="data">The text or URL content for the QR code.</param>
    /// <param name="moduleSize">The QR module pixel size (1 to 16). Defaults to 6.</param>
    /// <param name="errorCorrection">The error correction level. Defaults to <see cref="EscPosQrErrorCorrection.M"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ObjectDisposedException">This builder has already been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> encoded byte length exceeds 7089 bytes</exception>
    public EscPosBuilder QrCode(
        string data,
        int moduleSize = 6,
        EscPosQrErrorCorrection errorCorrection = EscPosQrErrorCorrection.M)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(data);

        var dataBytes = _encoding.GetBytes(data);
        if (dataBytes.Length > 7089)
        {
            throw new ArgumentException($"QR code data length ({dataBytes.Length} bytes) exceeds maximum Model 2 capacity of 7089 bytes.", nameof(data));
        }
        var length = dataBytes.Length + 3;

        // 1. Set Model 2
        _buffer.Write([0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00]);

        // 2. Set Module Size
        _buffer.Write([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)Math.Clamp(moduleSize, 1, 16)]);

        // 3. Set Error Correction Level
        _buffer.Write([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, (byte)errorCorrection]);

        // 4. Store Data
        var pL = (byte)(length & 0xFF);
        // Stryker disable all : Unsigned vs signed shift on positive integer produces identical byte
        var pH = (byte)((length >> 8) & 0xFF);
        // Stryker restore all
        _buffer.Write([0x1D, 0x28, 0x6B, pL, pH, 0x31, 0x50, 0x30]);
        _buffer.Write(dataBytes);

        // 5. Print QR Code
        _buffer.Write([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30]);

        return this;
    }

    /// <summary>
    /// Compiles all commands into an immutable <see cref="IPrintDocument"/>.
    /// </summary>
    /// <param name="documentName">The document name. Defaults to "Receipt".</param>
    /// <param name="idempotencyKey">An optional unique idempotency tracking key.</param>
    /// <returns>A compiled immutable <see cref="IPrintDocument"/>.</returns>
    public IPrintDocument Build(string documentName = "Receipt", string? idempotencyKey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new RawPrintDocument(_buffer.ToArray(), documentName, idempotencyKey);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _buffer.Dispose();
            _disposed = true;
        }
    }
}
