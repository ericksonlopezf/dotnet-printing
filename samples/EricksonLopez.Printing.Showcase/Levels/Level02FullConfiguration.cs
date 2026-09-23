// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO.Ports;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.Serial;
using EricksonLopez.Printing.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates the full configuration surface, Microsoft Dependency Injection integration, keyed services, and transport options.
/// </summary>
public static class Level02FullConfiguration
{
    /// <summary>
    /// Executes the full configuration showcase demonstration asynchronously.
    /// </summary>
    public static Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 02: FULL CONFIGURATION & DEPENDENCY INJECTION ARCHITECTURE");
        Console.WriteLine("================================================================================\n");

        var services = new ServiceCollection();

        // Registering logging
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // 1. Standard TCP Printer Client (Ephemeral socket per print)
        Console.WriteLine("[1] Registering Standard Ephemeral TCP Printer (AddTcpPrinter):");
        services.AddTcpPrinter(options =>
        {
            options.Host = "192.168.1.200";
            options.Port = 9100; // Standard RAW / JetDirect printing port
            options.TimeoutMs = 4000;
            options.NoDelay = true; // Disables Nagle algorithm to prevent 40ms buffering latency
            options.LingerSeconds = 0; // Releases TCP port immediately upon closure, preventing TIME_WAIT exhaustion
        });
        Console.WriteLine("    ✔ AddTcpPrinter configured with Host=192.168.1.200, Port=9100, NoDelay=true, LingerSeconds=0");

        // 2. Keyed TCP Printer Client (Multiple distinct network printers)
        Console.WriteLine("\n[2] Registering Keyed TCP Printer for specialized zone (AddKeyedTcpPrinter):");
        services.AddKeyedTcpPrinter("KitchenPrinter", options =>
        {
            options.Host = "192.168.1.201";
            options.Port = 9100;
            options.TimeoutMs = 6000;
        });
        Console.WriteLine("    ✔ AddKeyedTcpPrinter registered under key 'KitchenPrinter'");

        // 3. Pooled Persistent TCP Printer Client (High-volume POS)
        Console.WriteLine("\n[3] Registering High-Throughput Persistent Pooled TCP Printer (AddPooledTcpPrinter):");
        services.AddPooledTcpPrinter(options =>
        {
            options.Host = "192.168.1.202";
            options.Port = 9100;
            options.TimeoutMs = 5000;
            options.NoDelay = true;
        });
        Console.WriteLine("    ✔ AddPooledTcpPrinter registered as thread-safe singleton with persistent connection");

        // 4. Keyed Pooled TCP Printer Client
        Console.WriteLine("\n[4] Registering Keyed Persistent Pooled TCP Printer (AddKeyedPooledTcpPrinter):");
        services.AddKeyedPooledTcpPrinter("BarPrinter", options =>
        {
            options.Host = "192.168.1.203";
            options.Port = 9100;
            options.TimeoutMs = 5000;
        });
        Console.WriteLine("    ✔ AddKeyedPooledTcpPrinter registered under key 'BarPrinter'");

        // 5. Serial Port Printer Client (RS-232 / Virtual COM port)
        Console.WriteLine("\n[5] Registering Serial COM Port Printer (AddSerialPrinter):");
        services.AddSerialPrinter(options =>
        {
            options.PortName = "COM1";
            options.BaudRate = 115200;
            options.Parity = Parity.None;
            options.DataBits = 8;
            options.StopBits = StopBits.One;
            options.Handshake = Handshake.RequestToSend; // Hardware flow control
            options.WriteTimeoutMs = 3000;
            options.ReadTimeoutMs = 1500;
        });
        Console.WriteLine("    ✔ AddSerialPrinter configured with COM1, 115200 baud, 8N1, RTS handshake");

        // 6. Keyed Serial Port Printer
        Console.WriteLine("\n[6] Registering Keyed Serial Printer for Fiscal Register (AddKeyedSerialPrinter):");
        services.AddKeyedSerialPrinter("FiscalRegister", options =>
        {
            options.PortName = "COM2";
            options.BaudRate = 9600;
            options.Parity = Parity.Even;
            options.DataBits = 8;
            options.StopBits = StopBits.One;
        });
        Console.WriteLine("    ✔ AddKeyedSerialPrinter registered under key 'FiscalRegister'");

        // 7. SignalR Web-to-Edge Printing Bridge
        Console.WriteLine("\n[7] Registering SignalR Remote Printing Bridge (AddPrintingSignalR):");
        services.AddPrintingSignalR();
        Console.WriteLine("    ✔ AddPrintingSignalR registered HubPrinterDispatcher and SignalR dependencies");

        // 8. Building provider and verifying container resolution
        Console.WriteLine("\n[8] Resolving configured clients from IServiceProvider container:");
        using var provider = services.BuildServiceProvider();

        var defaultPrinter = provider.GetRequiredService<IPrinterClient>();
        var kitchenPrinter = provider.GetRequiredKeyedService<IPrinterClient>("KitchenPrinter");
        var barPrinter = provider.GetRequiredKeyedService<IPrinterClient>("BarPrinter");
        var fiscalPrinter = provider.GetRequiredKeyedService<IPrinterClient>("FiscalRegister");
        var signalRDispatcher = provider.GetRequiredService<IHubPrinterDispatcher>();

        Console.WriteLine($"    ✔ Default printer resolved: {defaultPrinter.GetType().Name}");
        Console.WriteLine($"    ✔ Keyed Kitchen printer resolved: {kitchenPrinter.GetType().Name}");
        Console.WriteLine($"    ✔ Keyed Bar printer resolved: {barPrinter.GetType().Name}");
        Console.WriteLine($"    ✔ Keyed Fiscal printer resolved: {fiscalPrinter.GetType().Name}");
        Console.WriteLine($"    ✔ SignalR Dispatcher resolved: {signalRDispatcher.GetType().Name}");

        Console.WriteLine("\n✔ Level 02 completed successfully.\n");
        return Task.CompletedTask;
    }
}
