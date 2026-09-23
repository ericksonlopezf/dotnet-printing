// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Printing;

/// <summary>Represents an in-memory print document backed by a raw byte array.</summary>
public sealed class RawPrintDocument : IPrintDocument
{
    private readonly byte[] _bytes;

    /// <inheritdoc />
    public string DocumentName { get; }

    /// <inheritdoc />
    public string? IdempotencyKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RawPrintDocument"/> class with the specified byte payload and metadata.
    /// </summary>
    /// <param name="bytes">The raw byte payload representing the print commands.</param>
    /// <param name="documentName">The human-readable name of the document.</param>
    /// <param name="idempotencyKey">An optional unique identifier for job deduplication.</param>
    /// <param name="isIdempotent"><see langword="true"/> if the document is safe to retransmit on failure; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> or <paramref name="documentName"/> is <see langword="null"/></exception>
    public RawPrintDocument(byte[] bytes, string documentName = "PrintJob", string? idempotencyKey = null, bool isIdempotent = false)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        DocumentName = documentName ?? throw new ArgumentNullException(nameof(documentName));
        IdempotencyKey = idempotencyKey;
        IsIdempotent = isIdempotent;
    }

    /// <inheritdoc />
    public byte[] GetBytes() => _bytes;

    /// <inheritdoc />
    public bool IsEmpty => _bytes.Length == 0;

    /// <inheritdoc />
    public bool IsIdempotent { get; }

    /// <inheritdoc />
    public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return stream.WriteAsync(_bytes, cancellationToken);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetMemory() => _bytes.AsMemory();
}
