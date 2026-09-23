// Copyright © Erickson Lopez. MIT License.
using System.Threading.Tasks;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Defines client-side operations invoked on printer agents connected via SignalR.
/// </summary>
public interface IPrinterHubClient
{
    /// <summary>
    /// Invoked when a print job is dispatched to the connected agent.
    /// </summary>
    /// <param name="job">The print job details and payload to be printed</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnPrintJobReceived(PrintJobMessage job);
}
