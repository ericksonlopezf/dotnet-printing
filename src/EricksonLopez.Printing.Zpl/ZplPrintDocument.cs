// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Printing.Zpl;

/// <summary>
/// Represents a ZPL print document that streams UTF-8 encoded Zebra Programming Language commands directly to an output stream.
/// </summary>
public sealed class ZplPrintDocument : IPrintDocument
{
    private readonly string _zplContent;
    private byte[]? _bytesCache;

    /// <inheritdoc />
    public string DocumentName { get; }

    /// <inheritdoc />
    public string? IdempotencyKey { get; }

    /// <inheritdoc />
    public bool IsEmpty => string.IsNullOrEmpty(_zplContent);

    /// <inheritdoc />
    public bool IsIdempotent { get; }

    internal ZplPrintDocument(string zplContent, string documentName = "ZebraLabel", string? idempotencyKey = null, bool isIdempotent = false)
    {
        _zplContent = zplContent;
        DocumentName = documentName ?? throw new ArgumentNullException(nameof(documentName));
        IdempotencyKey = idempotencyKey;
        IsIdempotent = isIdempotent;
    }

    /// <inheritdoc />
    public byte[] GetBytes()
    {
        if (_bytesCache == null)
        {
            _bytesCache = Encoding.UTF8.GetBytes(_zplContent);
        }
        return _bytesCache;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/></exception>
    public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var sw = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true);
        await sw.WriteAsync(_zplContent.AsMemory(), cancellationToken).ConfigureAwait(false);
        await sw.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
