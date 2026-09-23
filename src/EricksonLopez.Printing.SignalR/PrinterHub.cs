// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Provides a SignalR hub that manages connections and group memberships for local hardware printer agents.
/// </summary>
[Authorize]
public sealed class PrinterHub : Hub<IPrinterHubClient>
{
    /// <summary>
    /// Enrolls the calling client connection into the group for the specified printer identifier.
    /// </summary>
    /// <param name="printerName">The name or identifier of the printer to register</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException"><paramref name="printerName"/> is <see langword="null"/> or whitespace</exception>
    public async Task RegisterPrinter(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("Printer name cannot be null or whitespace.", nameof(printerName));
        }

        var groupName = GetPrinterGroupName(printerName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the calling client connection from the printer group.
    /// </summary>
    /// <param name="printerName">The name or identifier of the printer to unregister</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UnregisterPrinter(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return;
        }

        var groupName = GetPrinterGroupName(printerName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
    }

    /// <summary>
    /// Enrolls the calling client connection into the group for the specified printer identifier asynchronously.
    /// </summary>
    /// <param name="printerName">The name or identifier of the printer to register</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException"><paramref name="printerName"/> is <see langword="null"/> or whitespace</exception>
    public Task RegisterPrinterAsync(string printerName) => RegisterPrinter(printerName);

    /// <summary>
    /// Removes the calling client connection from the printer group asynchronously.
    /// </summary>
    /// <param name="printerName">The name or identifier of the printer to unregister</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task UnregisterPrinterAsync(string printerName) => UnregisterPrinter(printerName);

    internal static string GetPrinterGroupName(string printerName) =>
        // Group name format: printer:{PRINTERNAME_UPPERCASED}
        // The printer name is trimmed and normalized to uppercase so that registration is
        // case-insensitive. Edge clients must call RegisterPrinter() with a name (any casing)
        // that, when uppercased, matches the name used when dispatching via
        // IHubPrinterDispatcher.DispatchAsync. For example, RegisterPrinter("ReceiptA") and
        // RegisterPrinter("receipta") both resolve to the group "printer:RECEIPTA".
        $"printer:{printerName.Trim().ToUpperInvariant()}";
}
