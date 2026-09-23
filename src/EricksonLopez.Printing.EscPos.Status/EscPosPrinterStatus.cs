// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos.Status;

/// <summary>
/// Represents the parsed real-time hardware status of an ESC/POS printer.
/// </summary>
public sealed record EscPosPrinterStatus
{
    /// <summary>
    /// Gets a value indicating whether the printer is online and ready to accept print commands.
    /// </summary>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Gets a value indicating whether the printer roll cover is open.
    /// </summary>
    public bool IsCoverOpen { get; init; }

    /// <summary>
    /// Gets a value indicating whether paper has completely run out.
    /// </summary>
    public bool IsPaperOut { get; init; }

    /// <summary>
    /// Gets a value indicating whether the paper roll is near its end.
    /// </summary>
    public bool IsPaperNearEnd { get; init; }

    /// <summary>
    /// Gets a value indicating whether the connected cash drawer is open.
    /// </summary>
    public bool IsDrawerOpen { get; init; }

    /// <summary>
    /// Gets a value indicating whether an error condition is present on the printer.
    /// </summary>
    public bool HasError { get; init; }

    /// <summary>
    /// Gets a value indicating whether an automatic paper cutter error has occurred.
    /// </summary>
    public bool HasCutterError { get; init; }
}
