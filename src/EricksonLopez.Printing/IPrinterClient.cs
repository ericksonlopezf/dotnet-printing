// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.Printing;

/// <summary>Defines a client driver capable of transmitting print documents to a physical or network printer.</summary>
public interface IPrinterClient
{
    /// <summary>
    /// Transmits raw document commands asynchronously to the target printer.
    /// </summary>
    /// <param name="document">The print document payload to transmit.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a successful result if printed without transmission errors; otherwise, an error result detailing the failure.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default);
}
