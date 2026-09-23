// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos.Status;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates enterprise real-time hardware status queries, DLE EOT status decoding, and structured logging telemetry.
/// </summary>
public static class Level10EnterpriseArchitecture
{
    /// <summary>
    /// Executes the enterprise architecture and status telemetry showcase demonstration asynchronously.
    /// </summary>
    public static Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 10: ENTERPRISE ARCHITECTURE — HARDWARE STATUS & TELEMETRY");
        Console.WriteLine("================================================================================\n");

        // 1. ESC/POS Real-Time Status Query Commands (DLE EOT n)
        Console.WriteLine("[1] Inspecting Predefined DLE EOT Status Commands (EscPosStatusCommands):");
        Console.WriteLine($"    • QueryPrinterStatus (DLE EOT 1): {Convert.ToHexString(EscPosStatusCommands.QueryPrinterStatus.Span)} (Drawer & Online status)");
        Console.WriteLine($"    • QueryOfflineCause (DLE EOT 2):  {Convert.ToHexString(EscPosStatusCommands.QueryOfflineCause.Span)} (Cover, Paper, Error status)");
        Console.WriteLine($"    • QueryErrorStatus  (DLE EOT 3):  {Convert.ToHexString(EscPosStatusCommands.QueryErrorStatus.Span)} (Cutter & Fatal error status)");
        Console.WriteLine($"    • QueryPaperSensor  (DLE EOT 4):  {Convert.ToHexString(EscPosStatusCommands.QueryPaperSensor.Span)} (Roll Near-End & Paper-Out status)\n");

        // 2. Simulating and Decoding a Healthy Normal Printer Response
        Console.WriteLine("[2] Decoding Normal Healthy Printer Status via EscPosStatusParser.Parse():");
        // Byte 1: 0x12 (Bit 4=1 (fixed), Bit 1=1 (fixed)) -> Online, Drawer Closed
        // Byte 2: 0x12 (Bit 4=1 (fixed), Bit 1=1 (fixed)) -> Cover Closed, Paper Present, No Error
        // Byte 3: 0x12 (Bit 4=1 (fixed), Bit 1=1 (fixed)) -> No Cutter Error, No Unrecoverable Error
        // Byte 4: 0x12 (Bit 4=1 (fixed), Bit 1=1 (fixed)) -> Paper Adequate, Not Near End
        byte b1Healthy = 0b00010010;
        byte b2Healthy = 0b00010010;
        byte b3Healthy = 0b00010010;
        byte b4Healthy = 0b00010010;

        var healthyStatus = EscPosStatusParser.Parse(b1Healthy, b2Healthy, b3Healthy, b4Healthy);
        PrintStatusRecord("HEALTHY PRINTER", healthyStatus);

        // 3. Simulating and Decoding an Alert Condition (Cover Open + Paper Out + Drawer Open)
        Console.WriteLine("\n[3] Decoding Alert Conditions (Cover Open, Paper Out, Drawer Open):");
        // Byte 1: Bit 2=1 (Drawer open), Bit 3=1 (Offline)
        byte b1Alert = 0b00011110;
        // Byte 2: Bit 2=1 (Cover open), Bit 5=1 (Paper out), Bit 6=1 (Error)
        byte b2Alert = 0b01110110;
        // Byte 3: Bit 3=1 (Autocutter error)
        byte b3Alert = 0b00011010;
        // Byte 4: Bits 2,3=1 (Paper near end), Bits 5,6=1 (Paper out)
        byte b4Alert = 0b01111110;

        var alertStatus = EscPosStatusParser.Parse(b1Alert, b2Alert, b3Alert, b4Alert);
        PrintStatusRecord("WARNING/ERROR PRINTER", alertStatus);

        // 4. Testing Individual Byte Parser Methods
        Console.WriteLine("\n[4] Targeted Single-Byte Status Parsing Methods:");
        var (drawerOpen, isOffline) = EscPosStatusParser.ParsePrinterStatusByte(b1Alert);
        var (coverOpen, paperOut, hasErr) = EscPosStatusParser.ParseOfflineCauseByte(b2Alert);
        var (cutterErr, unrecov) = EscPosStatusParser.ParseErrorStatusByte(b3Alert);
        var (paperNearEnd, paperOutSensor) = EscPosStatusParser.ParsePaperSensorByte(b4Alert);

        Console.WriteLine($"    • ParsePrinterStatusByte: DrawerOpen={drawerOpen}, Offline={isOffline}");
        Console.WriteLine($"    • ParseOfflineCauseByte:  CoverOpen={coverOpen}, PaperOut={paperOut}, HasError={hasErr}");
        Console.WriteLine($"    • ParseErrorStatusByte:   CutterError={cutterErr}, Unrecoverable={unrecov}");
        Console.WriteLine($"    • ParsePaperSensorByte:   PaperNearEnd={paperNearEnd}, PaperOut={paperOutSensor}\n");

        // 5. Structured Telemetry & Event IDs
        Console.WriteLine("[5] Structured Logging Event ID Architecture (PrintingEventIds):");
        Console.WriteLine($"    • {PrintingEventIds.TcpPrintStarted.Id}: {PrintingEventIds.TcpPrintStarted.Name}");
        Console.WriteLine($"    • {PrintingEventIds.TcpPrintSuccess.Id}: {PrintingEventIds.TcpPrintSuccess.Name}");
        Console.WriteLine($"    • {PrintingEventIds.TcpPrintFailed.Id}:  {PrintingEventIds.TcpPrintFailed.Name}");
        Console.WriteLine($"    • {PrintingEventIds.SerialPrintStarted.Id}: {PrintingEventIds.SerialPrintStarted.Name}");
        Console.WriteLine($"    • {PrintingEventIds.SerialPrintSuccess.Id}: {PrintingEventIds.SerialPrintSuccess.Name}");
        Console.WriteLine($"    • {PrintingEventIds.SerialPrintFailed.Id}:  {PrintingEventIds.SerialPrintFailed.Name}");
        Console.WriteLine($"    • {PrintingEventIds.RetryAttempt.Id}:   {PrintingEventIds.RetryAttempt.Name}");
        Console.WriteLine($"    • {PrintingEventIds.RetryExhausted.Id}: {PrintingEventIds.RetryExhausted.Name}");
        Console.WriteLine("    ✔ Standardized Event IDs enable zero-allocation high-performance structured telemetry in production.");

        Console.WriteLine("\n✔ Level 10 completed successfully.\n");
        return Task.CompletedTask;
    }

    private static void PrintStatusRecord(string header, EscPosPrinterStatus status)
    {
        Console.WriteLine($"    === {header} ===");
        Console.WriteLine($"    • IsOnline:        {status.IsOnline}");
        Console.WriteLine($"    • IsCoverOpen:     {status.IsCoverOpen}");
        Console.WriteLine($"    • IsPaperOut:      {status.IsPaperOut}");
        Console.WriteLine($"    • IsPaperNearEnd:  {status.IsPaperNearEnd}");
        Console.WriteLine($"    • IsDrawerOpen:    {status.IsDrawerOpen}");
        Console.WriteLine($"    • HasError:        {status.HasError}");
        Console.WriteLine($"    • HasCutterError:  {status.HasCutterError}");
    }
}
