// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates cloud-to-edge remote printing architecture using ASP.NET Core SignalR and HubPrinterDispatcher.
/// </summary>
public static class Level09RemoteDispatchSignalR
{
    /// <summary>
    /// Executes the SignalR remote dispatch showcase demonstration asynchronously.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 09: REMOTE DISPATCH — CLOUD-TO-EDGE SIGNALR ARCHITECTURE");
        Console.WriteLine("================================================================================\n");

        Console.WriteLine("1. ARCHITECTURE OVERVIEW:");
        Console.WriteLine("   • Cloud / SaaS Server: Hosts PrinterHub with [Authorize] and IHubPrinterDispatcher.");
        Console.WriteLine("   • Edge Agent (POS / Raspberry Pi): Connects via WebSocket, enrolls in group via RegisterPrinter('FrontDesk').");
        Console.WriteLine("   • On Dispatch: Document bytes are Base64 encoded into PrintJobMessage and broadcasted to group.");
        Console.WriteLine("   • Edge Reception: Client receives OnPrintJobReceived, decodes bytes, and prints to physical hardware.\n");

        // 2. Simulating the Edge Agent implementing IPrinterHubClient
        var localPhysicalPrinter = new SimulatedEdgeHardwarePrinter();
        var edgeAgent = new EdgeStorePrinterAgent("FrontDesk", localPhysicalPrinter);

        // 3. Simulating Server Dispatch via HubPrinterDispatcher
        var mockHubContext = new MockHubContext(edgeAgent);
        var dispatcher = new HubPrinterDispatcher(mockHubContext);

        // 4. Composing a print job on the server
        var serverReceipt = new RawPrintDocument(
            bytes: Encoding.UTF8.GetBytes("ONLINE ORDER #CLOUD-771\n1x Margherita Pizza\n1x Soda 500ml\n"),
            documentName: "CloudOnlineOrder",
            idempotencyKey: "JOB-CLOUD-771",
            isIdempotent: true);

        Console.WriteLine("[2] Dispatching print job from Cloud Backend via IHubPrinterDispatcher:");
        var dispatchResult = await dispatcher.DispatchAsync("FrontDesk", serverReceipt);

        Console.WriteLine($"    Dispatch Result: IsSuccess={dispatchResult.IsSuccess}, Value={dispatchResult.Value}");
        Console.WriteLine($"    Edge Agent Jobs Received: {edgeAgent.ReceivedCount}");
        Console.WriteLine($"    Edge Physical Hardware Printed: {localPhysicalPrinter.PrintedJobsCount}");
        Console.WriteLine($"    Last Physical Output Decoded: '{localPhysicalPrinter.LastPrintedString?.Trim()}'\n");

        // 5. Demonstrating Validation of Empty Printer Name
        Console.WriteLine("[3] Dispatch Validation Protection:");
        var invalidDispatch = await dispatcher.DispatchAsync("", serverReceipt);
        Console.WriteLine($"    Empty name rejected: IsFailure={invalidDispatch.IsFailure}, Code='{invalidDispatch.Error.Code}'");
        Console.WriteLine($"    Description: {invalidDispatch.Error.Description}");

        Console.WriteLine("\n✔ Level 09 completed successfully.\n");
    }

    /// <summary>
    /// Represents an edge client daemon running in a retail store connected to the cloud hub.
    /// </summary>
    private sealed class EdgeStorePrinterAgent : IPrinterHubClient
    {
        private readonly string _printerName;
        private readonly IPrinterClient _hardwareClient;
        public int ReceivedCount { get; private set; }

        public EdgeStorePrinterAgent(string printerName, IPrinterClient hardwareClient)
        {
            _printerName = printerName;
            _hardwareClient = hardwareClient;
        }

        public async Task OnPrintJobReceived(PrintJobMessage job)
        {
            ReceivedCount++;
            Console.WriteLine($"    [EdgeAgent] Received PrintJobMessage: JobId='{job.JobId}', Printer='{job.PrinterName}', Doc='{job.DocumentName}'");

            // Decode Base64 payload back to raw printer commands
            var rawBytes = Convert.FromBase64String(job.PayloadBase64);
            var localDoc = new RawPrintDocument(rawBytes, job.DocumentName, job.JobId, isIdempotent: true);

            // Transmit to local physical printer
            var printResult = await _hardwareClient.PrintAsync(localDoc);
            Console.WriteLine($"    [EdgeAgent] Local hardware transmission: Success={printResult.IsSuccess}");
        }
    }

    private sealed class SimulatedEdgeHardwarePrinter : IPrinterClient
    {
        public int PrintedJobsCount { get; private set; }
        public string? LastPrintedString { get; private set; }

        public Task<EricksonLopez.Result.Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
        {
            PrintedJobsCount++;
            LastPrintedString = Encoding.UTF8.GetString(document.GetBytes());
            return Task.FromResult(EricksonLopez.Result.Result<bool>.Success(true));
        }
    }

    private sealed class MockHubContext : IHubContext<PrinterHub, IPrinterHubClient>
    {
        private readonly IPrinterHubClient _client;

        private readonly IHubClients<IPrinterHubClient> _clients;

        public MockHubContext(IPrinterHubClient client)
        {
            _client = client;
            _clients = new MockHubClients(client);
            Groups = new MockGroupManager();
        }

        public IHubClients<IPrinterHubClient> Clients
        {
#if NET9_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("SignalR client proxy generation requires dynamic code.")]
#endif
            get => _clients;
        }

        public IGroupManager Groups { get; }
    }

    private sealed class MockHubClients : IHubClients<IPrinterHubClient>
    {
        private readonly IPrinterHubClient _client;

        public MockHubClients(IPrinterHubClient client)
        {
            _client = client;
        }

        public IPrinterHubClient All => _client;
        public IPrinterHubClient AllExcept(System.Collections.Generic.IReadOnlyList<string> excludedConnectionIds) => _client;
        public IPrinterHubClient Client(string connectionId) => _client;
        public IPrinterHubClient Clients(System.Collections.Generic.IReadOnlyList<string> connectionIds) => _client;
        public IPrinterHubClient Group(string groupName) => _client;
        public IPrinterHubClient GroupExcept(string groupName, System.Collections.Generic.IReadOnlyList<string> excludedConnectionIds) => _client;
        public IPrinterHubClient Groups(System.Collections.Generic.IReadOnlyList<string> groupNames) => _client;
        public IPrinterHubClient User(string userId) => _client;
        public IPrinterHubClient Users(System.Collections.Generic.IReadOnlyList<string> userIds) => _client;
    }

    private sealed class MockGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
