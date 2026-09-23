// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;

namespace EricksonLopez.Printing.Zpl;

/// <summary>
/// Provides a fluent builder for compiling Zebra Programming Language (ZPL II) industrial labels.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Builder instances must not be shared across concurrent threads.
/// The builder is designed for single-threaded sequential composition of one label per instance.
/// The output <see cref="IPrintDocument"/> produced by <see cref="Build"/> is immutable and safe for concurrent use.
/// </remarks>
public sealed class ZplBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly bool _safeMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZplBuilder"/> class and begins a new label format (<c>^XA</c>).
    /// </summary>
    /// <param name="safeMode"><see langword="true"/> to disallow raw command injection; otherwise, <see langword="false"/>.</param>
    public ZplBuilder(bool safeMode = false)
    {
        _safeMode = safeMode;
        _sb.AppendLine("^XA");
    }

    /// <summary>
    /// Appends raw ZPL command strings directly to the label buffer.
    /// </summary>
    /// <remarks>
    /// Using this method allows raw injection of ZPL commands which can be used to reconfigure the physical printer. Untrusted input must never be passed to this method.
    /// </remarks>
    /// <param name="rawZpl">The raw ZPL commands to append</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException">The builder was initialized with safe mode enabled</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rawZpl"/> is <see langword="null"/></exception>
    public ZplBuilder Raw(string rawZpl)
    {
        if (_safeMode)
        {
            throw new NotSupportedException("Raw ZPL commands are not allowed in safe mode to prevent ZPL injection.");
        }
        ArgumentNullException.ThrowIfNull(rawZpl);
        _sb.Append(rawZpl);
        return this;
    }

    /// <summary>
    /// Adds a text field at the specified coordinates using a character font identifier.
    /// </summary>
    /// <param name="x">The x-coordinate of the text field in dots</param>
    /// <param name="y">The y-coordinate of the text field in dots</param>
    /// <param name="text">The text content to render</param>
    /// <param name="height">The character height in dots. Must be greater than zero.</param>
    /// <param name="width">The character width in dots. Must be greater than zero.</param>
    /// <param name="font">The ZPL font identifier character. Default is '0'. Valid values are '0' and 'A'–'H' per the ZPL II specification.</param>
    /// <param name="orientation">The text orientation. Default is <see cref="ZplOrientation.Normal"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload accepts any <see langword="char"/> value without validation. An invalid font character
    /// produces a syntactically well-formed but semantically incorrect ZPL label. Prefer the
    /// <see cref="Text(int, int, string, ZplFont, int, int, ZplOrientation)"/> overload which constrains
    /// the font to the <see cref="ZplFont"/> enumeration values.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> or <paramref name="width"/> is less than or equal to zero</exception>
    public ZplBuilder Text(
        int x,
        int y,
        string text,
        int height = 30,
        int width = 30,
        char font = '0',
        ZplOrientation orientation = ZplOrientation.Normal)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append("^A").Append(font).Append((char)orientation).Append(',').Append(height).Append(',').Append(width);
        AppendEscapedZplFieldData(_sb, text);
        _sb.AppendLine();
        return this;
    }

    /// <summary>
    /// Adds a text field at the specified coordinates using a strongly-typed <see cref="ZplFont"/>.
    /// </summary>
    /// <param name="x">The x-coordinate of the text field in dots</param>
    /// <param name="y">The y-coordinate of the text field in dots</param>
    /// <param name="text">The text content to render</param>
    /// <param name="font">The strongly-typed ZPL font identifier</param>
    /// <param name="height">The character height in dots. Must be greater than zero.</param>
    /// <param name="width">The character width in dots. Must be greater than zero.</param>
    /// <param name="orientation">The text orientation. Default is <see cref="ZplOrientation.Normal"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> or <paramref name="width"/> is less than or equal to zero</exception>
    public ZplBuilder Text(
        int x,
        int y,
        string text,
        ZplFont font,
        int height = 30,
        int width = 30,
        ZplOrientation orientation = ZplOrientation.Normal)
        => Text(x, y, text, height, width, (char)font, orientation);

    /// <summary>
    /// Adds a Code 128 barcode at the specified coordinates (<c>^BC</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the barcode in dots</param>
    /// <param name="y">The y-coordinate of the barcode in dots</param>
    /// <param name="data">The barcode data string</param>
    /// <param name="height">The barcode height in dots. Must be greater than zero.</param>
    /// <param name="showText"><see langword="true"/> to print the human-readable interpretation line; otherwise, <see langword="false"/>.</param>
    /// <param name="orientation">The barcode orientation. Default is <see cref="ZplOrientation.Normal"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="data"/> is <see langword="null"/>, empty, whitespace, or contains ZPL command delimiters</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> is less than or equal to zero</exception>
    public ZplBuilder BarcodeCode128(
        int x,
        int y,
        string data,
        int height = 60,
        bool showText = true,
        ZplOrientation orientation = ZplOrientation.Normal)
    {
        var cmd = $"^BC{(char)orientation},{height},{(showText ? 'Y' : 'N')},N,N";
        return AppendBarcode(x, y, data, height, cmd);
    }

    /// <summary>
    /// Adds a Code 39 barcode at the specified coordinates (<c>^B3</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the barcode in dots</param>
    /// <param name="y">The y-coordinate of the barcode in dots</param>
    /// <param name="data">The barcode data string</param>
    /// <param name="height">The barcode height in dots. Must be greater than zero.</param>
    /// <param name="showText"><see langword="true"/> to print the human-readable interpretation line; otherwise, <see langword="false"/>.</param>
    /// <param name="orientation">The barcode orientation. Default is <see cref="ZplOrientation.Normal"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="data"/> is <see langword="null"/>, empty, whitespace, or contains ZPL command delimiters</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> is less than or equal to zero</exception>
    public ZplBuilder BarcodeCode39(
        int x,
        int y,
        string data,
        int height = 60,
        bool showText = true,
        ZplOrientation orientation = ZplOrientation.Normal)
    {
        var cmd = $"^B3{(char)orientation},N,{height},{(showText ? 'Y' : 'N')},N";
        return AppendBarcode(x, y, data, height, cmd);
    }

    /// <summary>
    /// Adds an EAN-13 barcode at the specified coordinates (<c>^BE</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the barcode in dots</param>
    /// <param name="y">The y-coordinate of the barcode in dots</param>
    /// <param name="data">The barcode data string</param>
    /// <param name="height">The barcode height in dots. Must be greater than zero.</param>
    /// <param name="showText"><see langword="true"/> to print the human-readable interpretation line; otherwise, <see langword="false"/>.</param>
    /// <param name="orientation">The barcode orientation. Default is <see cref="ZplOrientation.Normal"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="data"/> is <see langword="null"/>, empty, whitespace, or contains ZPL command delimiters</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> is less than or equal to zero</exception>
    public ZplBuilder BarcodeEan13(
        int x,
        int y,
        string data,
        int height = 60,
        bool showText = true,
        ZplOrientation orientation = ZplOrientation.Normal)
    {
        var cmd = $"^BE{(char)orientation},{height},{(showText ? 'Y' : 'N')},N";
        return AppendBarcode(x, y, data, height, cmd);
    }

    private ZplBuilder AppendBarcode(
        int x,
        int y,
        string data,
        int height,
        string barcodeCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ValidateBarcodeData(data);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append(barcodeCommand)
           .Append("^FD").Append(data).AppendLine("^FS");
        return this;
    }

    /// <summary>
    /// Adds a two-dimensional QR code at the specified coordinates (<c>^BQ</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the QR code in dots</param>
    /// <param name="y">The y-coordinate of the QR code in dots</param>
    /// <param name="data">The payload text or URL to encode in the QR code</param>
    /// <param name="magnification">The magnification factor between 1 and 10. Default is 4.</param>
    /// <param name="orientation">The QR code orientation. Default is <see cref="ZplOrientation.Normal"/>.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/></exception>
    public ZplBuilder QrCode(
        int x,
        int y,
        string data,
        int magnification = 4,
        ZplOrientation orientation = ZplOrientation.Normal)
    {
        ArgumentNullException.ThrowIfNull(data);
        magnification = Math.Clamp(magnification, 1, 10);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append("^BQ").Append((char)orientation).Append(",2,").Append(magnification);
        if (data.Contains('^') || data.Contains('~'))
        {
            _sb.Append("^FH_^FDQA,");
            foreach (var ch in data)
            {
                switch (ch)
                {
                    case '_': _sb.Append("_5F"); break;
                    case '^': _sb.Append("_5E"); break;
                    case '~': _sb.Append("_7E"); break;
                    default: _sb.Append(ch); break;
                }
            }
            _sb.AppendLine("^FS");
        }
        else
        {
            _sb.Append("^FDQA,").Append(data).AppendLine("^FS");
        }
        return this;
    }

    /// <summary>
    /// Draws a graphic box or line at the specified coordinates (<c>^GB</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the box in dots</param>
    /// <param name="y">The y-coordinate of the box in dots</param>
    /// <param name="width">The box width in dots. Must be non-negative.</param>
    /// <param name="height">The box height in dots. Must be non-negative.</param>
    /// <param name="borderThickness">The border line thickness in dots. Default is 2. Must be non-negative.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/>, <paramref name="height"/>, or <paramref name="borderThickness"/> is negative</exception>
    public ZplBuilder Box(int x, int y, int width, int height, int borderThickness = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(borderThickness);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append("^GB").Append(width).Append(',').Append(height).Append(',').Append(borderThickness).AppendLine("^FS");
        return this;
    }

    /// <summary>
    /// Draws a graphic circle at the specified coordinates (<c>^GC</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the circle center in dots</param>
    /// <param name="y">The y-coordinate of the circle center in dots</param>
    /// <param name="diameter">The circle diameter in dots. Must be non-negative.</param>
    /// <param name="borderThickness">The border line thickness in dots. Default is 2. Must be non-negative.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="diameter"/> or <paramref name="borderThickness"/> is negative</exception>
    public ZplBuilder Circle(int x, int y, int diameter, int borderThickness = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(diameter);
        ArgumentOutOfRangeException.ThrowIfNegative(borderThickness);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append("^GC").Append(diameter).Append(',').Append(borderThickness).AppendLine(",B^FS");
        return this;
    }

    /// <summary>
    /// Draws a graphic ellipse at the specified coordinates (<c>^GE</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the ellipse in dots</param>
    /// <param name="y">The y-coordinate of the ellipse in dots</param>
    /// <param name="width">The ellipse width in dots. Must be non-negative.</param>
    /// <param name="height">The ellipse height in dots. Must be non-negative.</param>
    /// <param name="borderThickness">The border line thickness in dots. Default is 2. Must be non-negative.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/>, <paramref name="height"/>, or <paramref name="borderThickness"/> is negative</exception>
    public ZplBuilder Ellipse(int x, int y, int width, int height, int borderThickness = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(borderThickness);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append("^GE").Append(width).Append(',').Append(height).Append(',').Append(borderThickness).AppendLine(",B^FS");
        return this;
    }

    /// <summary>
    /// Draws a graphic diagonal line at the specified coordinates (<c>^GD</c>).
    /// </summary>
    /// <param name="x">The x-coordinate of the diagonal bounding box in dots</param>
    /// <param name="y">The y-coordinate of the diagonal bounding box in dots</param>
    /// <param name="width">The bounding box width in dots. Must be non-negative.</param>
    /// <param name="height">The bounding box height in dots. Must be non-negative.</param>
    /// <param name="borderThickness">The line thickness in dots. Default is 2. Must be non-negative.</param>
    /// <param name="rightLeaning"><see langword="true"/> for a right-leaning diagonal (/); <see langword="false"/> for a left-leaning diagonal (\).</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/>, <paramref name="height"/>, or <paramref name="borderThickness"/> is negative</exception>
    public ZplBuilder DiagonalLine(int x, int y, int width, int height, int borderThickness = 2, bool rightLeaning = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(borderThickness);

        _sb.Append("^FO").Append(x).Append(',').Append(y)
           .Append("^GD").Append(width).Append(',').Append(height).Append(',').Append(borderThickness)
           .Append(",B,").Append(rightLeaning ? 'R' : 'L').AppendLine("^FS");
        return this;
    }

    /// <summary>
    /// Compiles all configured ZPL commands into an immutable <see cref="IPrintDocument"/>.
    /// </summary>
    /// <param name="documentName">The document name for telemetry and spool tracking. Default is &quot;ZebraLabel&quot;.</param>
    /// <param name="idempotencyKey">The optional unique idempotency tracking key</param>
    /// <returns>An immutable <see cref="IPrintDocument"/> containing the complete ZPL II label stream.</returns>
    public IPrintDocument Build(string documentName = "ZebraLabel", string? idempotencyKey = null)
    {
        var content = _sb.ToString() + "^XZ" + Environment.NewLine;
        return new ZplPrintDocument(content, documentName, idempotencyKey);
    }

    private static void AppendEscapedZplFieldData(StringBuilder sb, string text)
    {
        if (text.Contains('^') || text.Contains('~'))
        {
            sb.Append("^FH_^FD");
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '_': sb.Append("_5F"); break;
                    case '^': sb.Append("_5E"); break;
                    case '~': sb.Append("_7E"); break;
                    default: sb.Append(ch); break;
                }
            }
            sb.Append("^FS");
        }
        else
        {
            sb.Append("^FD").Append(text).Append("^FS");
        }
    }

    private static void ValidateBarcodeData(string data)
    {
        if (data.Contains('^') || data.Contains('~'))
        {
            throw new ArgumentException("Barcode data cannot contain ZPL command delimiters '^' or '~'.", nameof(data));
        }
    }
}
