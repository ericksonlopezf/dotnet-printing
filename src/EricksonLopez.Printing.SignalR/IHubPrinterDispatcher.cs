// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Defines a contract for dispatching print documents to edge printer agents connected via SignalR.
/// </summary>
public interface IHubPrinterDispatcher
{
    /// <summary>
    /// Dispatches a print document to all edge agents registered for the specified printer.
    /// </summary>
    /// <param name="printerName">The target printer name or identifier</param>
    /// <param name="document">The print document payload to transmit</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a successful result if dispatched; otherwise, a structured error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/></exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/></exception>
    Task<Result<bool>> DispatchAsync(
        string printerName,
        IPrintDocument document,
        CancellationToken cancellationToken = default);
}
