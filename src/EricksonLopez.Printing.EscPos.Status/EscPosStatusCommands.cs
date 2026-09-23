// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos.Status;

using System;

/// <summary>
/// Provides predefined command bytecode sequences for real-time status queries.
/// </summary>
public static class EscPosStatusCommands
{
    private static readonly byte[] _queryPrinterStatusBytes = [0x10, 0x04, 0x01];
    private static readonly byte[] _queryOfflineCauseBytes = [0x10, 0x04, 0x02];
    private static readonly byte[] _queryErrorStatusBytes = [0x10, 0x04, 0x03];
    private static readonly byte[] _queryPaperSensorBytes = [0x10, 0x04, 0x04];

    /// <summary>
    /// Gets the command bytes used to query general printer and cash drawer status.
    /// </summary>
    public static ReadOnlyMemory<byte> QueryPrinterStatus => _queryPrinterStatusBytes;

    /// <summary>
    /// Gets the command bytes used to query offline status causes such as cover open or paper out.
    /// </summary>
    public static ReadOnlyMemory<byte> QueryOfflineCause => _queryOfflineCauseBytes;

    /// <summary>
    /// Gets the command bytes used to query printer hardware error conditions.
    /// </summary>
    public static ReadOnlyMemory<byte> QueryErrorStatus => _queryErrorStatusBytes;

    /// <summary>
    /// Gets the command bytes used to query roll paper sensor levels.
    /// </summary>
    public static ReadOnlyMemory<byte> QueryPaperSensor => _queryPaperSensorBytes;
}
