// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos.Status;

/// <summary>
/// Provides parsing methods for decoding single-byte responses from ESC/POS real-time status queries.
/// </summary>
/// <remarks>
/// <para>Usage pattern (serial port or raw TCP bidirectional stream):</para>
/// <list type="number">
///   <item><description>Send <see cref="EscPosStatusCommands.QueryPrinterStatus"/>, <see cref="EscPosStatusCommands.QueryOfflineCause"/>, <see cref="EscPosStatusCommands.QueryErrorStatus"/>, and <see cref="EscPosStatusCommands.QueryPaperSensor"/> to the printer stream.</description></item>
///   <item><description>Read one byte response for each command (4 bytes total).</description></item>
///   <item><description>Pass the four bytes to <see cref="Parse"/> to obtain a unified <see cref="EscPosPrinterStatus"/> model.</description></item>
/// </list>
/// <para>Alternatively, use the individual parse methods for targeted single-query scenarios.</para>
/// </remarks>
public static class EscPosStatusParser
{
    /// <summary>
    /// Parses the single-byte response from a real-time printer status query.
    /// </summary>
    /// <param name="response">The response byte from the printer.</param>
    /// <returns>A tuple indicating whether the drawer is open and whether the printer is offline.</returns>
    public static (bool IsDrawerOpen, bool IsOffline) ParsePrinterStatusByte(byte response)
    {
        var isDrawerOpen = (response & 0x04) != 0; // Bit 2
        var isOffline = (response & 0x08) != 0;    // Bit 3
        return (isDrawerOpen, isOffline);
    }

    /// <summary>
    /// Parses the single-byte response from an offline cause status query.
    /// </summary>
    /// <param name="response">The response byte from the printer.</param>
    /// <returns>A tuple indicating whether the cover is open, a paper-out condition (paper has run out) is present, and whether an error flag is set.</returns>
    public static (bool IsCoverOpen, bool IsPaperOut, bool HasError) ParseOfflineCauseByte(byte response)
    {
        var isCoverOpen = (response & 0x04) != 0; // Bit 2
        var isPaperOut = (response & 0x20) != 0;  // Bit 5
        var hasError = (response & 0x40) != 0;    // Bit 6
        return (isCoverOpen, isPaperOut, hasError);
    }

    /// <summary>
    /// Parses the single-byte response from an error status query.
    /// </summary>
    /// <param name="response">The response byte from the printer.</param>
    /// <returns>A tuple indicating whether an autocutter error or unrecoverable error occurred.</returns>
    public static (bool HasCutterError, bool HasUnrecoverableError) ParseErrorStatusByte(byte response)
    {
        var hasCutterError = (response & 0x08) != 0;       // Bit 3
        var hasUnrecoverable = (response & 0x20) != 0;     // Bit 5
        return (hasCutterError, hasUnrecoverable);
    }

    /// <summary>
    /// Parses the single-byte response from a roll paper sensor status query.
    /// </summary>
    /// <param name="response">The response byte from the printer.</param>
    /// <returns>A tuple indicating whether paper is near its end or completely exhausted.</returns>
    public static (bool IsPaperNearEnd, bool IsPaperOut) ParsePaperSensorByte(byte response)
    {
        var isPaperNearEnd = (response & 0x0C) == 0x0C; // Bits 2 and 3
        var isPaperOut = (response & 0x60) == 0x60;     // Bits 5 and 6
        return (isPaperNearEnd, isPaperOut);
    }

    /// <summary>
    /// Combines four status query response bytes into a unified <see cref="EscPosPrinterStatus"/> domain model.
    /// </summary>
    /// <param name="statusByte1">The response byte from the printer status query.</param>
    /// <param name="statusByte2">The response byte from the offline cause status query.</param>
    /// <param name="statusByte3">The response byte from the error status query.</param>
    /// <param name="statusByte4">The response byte from the paper sensor status query.</param>
    /// <returns>A parsed <see cref="EscPosPrinterStatus"/> instance representing the combined printer state.</returns>
    public static EscPosPrinterStatus Parse(byte statusByte1, byte statusByte2, byte statusByte3, byte statusByte4)
    {
        var (drawerOpen, offline1) = ParsePrinterStatusByte(statusByte1);
        var (coverOpen, paperOut2, error2) = ParseOfflineCauseByte(statusByte2);
        var (cutterError, unrecoverable) = ParseErrorStatusByte(statusByte3);
        var (paperNearEnd, paperOut4) = ParsePaperSensorByte(statusByte4);

        var isPaperOut = paperOut2 || paperOut4;
        var hasError = error2 || cutterError || unrecoverable;
        var isOnline = !offline1 && !coverOpen && !isPaperOut && !hasError;

        return new EscPosPrinterStatus
        {
            IsOnline = isOnline,
            IsCoverOpen = coverOpen,
            IsPaperOut = isPaperOut,
            IsPaperNearEnd = paperNearEnd,
            IsDrawerOpen = drawerOpen,
            HasError = hasError,
            HasCutterError = cutterError
        };
    }
}
