// Copyright © Erickson Lopez. MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Printing;

/// <summary>Defines a print document payload containing raw printer control sequences.</summary>
public interface IPrintDocument
{
    /// <summary>Gets the human-readable name of the document or print job.</summary>
    string DocumentName { get; }

    /// <summary>Gets the raw binary commands to send to the printer.</summary>
    /// <returns>A byte array containing the raw printer commands.</returns>
    byte[] GetBytes();

    /// <summary>
    /// Gets a value indicating whether this print document contains no printable data.
    /// </summary>
    /// <remarks>
    /// Used to prevent zero-length submissions from consuming memory or network resources.
    /// </remarks>
    bool IsEmpty => false;

    /// <summary>
    /// Gets a value indicating whether this print document is safe to retry on transmission failure.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, retries on transmission errors are forbidden to prevent duplicate physical printing.
    /// </remarks>
    bool IsIdempotent => false;

    /// <summary>
    /// Writes the raw binary commands to the specified network or hardware stream.
    /// </summary>
    /// <param name="stream">The destination stream to receive the raw command bytes.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous write operation.
    /// </returns>
    ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw binary commands to send to the printer as a read-only memory region.
    /// </summary>
    /// <returns>
    /// A <see cref="ReadOnlyMemory{T}"/> wrapping the document bytes.
    /// </returns>
    /// <remarks>
    /// Implementations should override this method to provide a true zero-copy view when backed by a
    /// pre-allocated buffer (e.g., <see cref="RawPrintDocument"/> returns <c>_bytes.AsMemory()</c>).
    /// The default implementation delegates to <see cref="GetBytes()"/> and therefore does not eliminate
    /// array allocation on its own.
    /// </remarks>
    ReadOnlyMemory<byte> GetMemory() => GetBytes();

    /// <summary>
    /// Gets an optional unique idempotency key identifying this print job for deduplication across network and physical retries.
    /// </summary>
    string? IdempotencyKey => null;
}
