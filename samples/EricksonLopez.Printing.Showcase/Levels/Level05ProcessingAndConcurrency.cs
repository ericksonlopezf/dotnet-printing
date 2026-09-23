// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Printing;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates high-throughput concurrency, socket pooling vs ephemeral connections, serialized access, and cooperative cancellation.
/// </summary>
public static class Level05ProcessingAndConcurrency
{
    /// <summary>
    /// Executes the processing and concurrency showcase demonstration asynchronously.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 05: PROCESSING & CONCURRENCY — PERSISTENT POOLING & SERIALIZATION");
        Console.WriteLine("================================================================================\n");

        // Start a local loopback TCP listener to act as a mock physical printer
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var listenerCts = new CancellationTokenSource();

        // Background server loop draining incoming print bytes
        var serverTask = Task.Run(async () =>
        {
            try
            {
                while (!listenerCts.Token.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(listenerCts.Token);
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        await using (var stream = client.GetStream())
                        {
                            var buffer = new byte[4096];
                            while (await stream.ReadAsync(buffer, listenerCts.Token) > 0)
                            {
                                // Consume bytes like a physical printer
                            }
                        }
                    }, listenerCts.Token);
                }
            }
            catch (OperationCanceledException) { }
        });

        try
        {
            var options = new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000,
                NoDelay = true,
                LingerSeconds = 0
            };

            const int totalJobs = 20;
            var testDocument = new RawPrintDocument(
                bytes: [0x1B, 0x40, 0x54, 0x65, 0x73, 0x74, 0x0A],
                documentName: "ConcurrentJob",
                isIdempotent: true);

            // 1. Testing Ephemeral TcpPrinterClient under concurrent burst
            Console.WriteLine($"[1] Executing {totalJobs} concurrent print jobs via Ephemeral TcpPrinterClient:");
            var ephemeralClient = new TcpPrinterClient(options);
            var swEphemeral = Stopwatch.StartNew();

            var ephemeralTasks = new List<Task<EricksonLopez.Result.Result<bool>>>();
            for (int i = 0; i < totalJobs; i++)
            {
                ephemeralTasks.Add(Task.Run(() => ephemeralClient.PrintAsync(testDocument)));
            }

            var ephemeralResults = await Task.WhenAll(ephemeralTasks);
            swEphemeral.Stop();

            var ephemeralSuccesses = 0;
            foreach (var res in ephemeralResults)
            {
                if (res.IsSuccess)
                {
                    ephemeralSuccesses++;
                }
            }
            Console.WriteLine($"    ✔ Ephemeral completed: {ephemeralSuccesses}/{totalJobs} succeeded in {swEphemeral.ElapsedMilliseconds} ms.");
            Console.WriteLine("      (Notice: Creates 20 independent TCP sockets and handshakes)\n");

            // 2. Testing Persistent PooledTcpPrinterClient under concurrent burst
            Console.WriteLine($"[2] Executing {totalJobs} concurrent print jobs via Persistent PooledTcpPrinterClient:");
            await using (var pooledClient = new PooledTcpPrinterClient(options))
            {
                var swPooled = Stopwatch.StartNew();

                var pooledTasks = new List<Task<EricksonLopez.Result.Result<bool>>>();
                for (int i = 0; i < totalJobs; i++)
                {
                    pooledTasks.Add(Task.Run(() => pooledClient.PrintAsync(testDocument)));
                }

                var pooledResults = await Task.WhenAll(pooledTasks);
                swPooled.Stop();

                var pooledSuccesses = 0;
                foreach (var res in pooledResults)
                {
                    if (res.IsSuccess)
                    {
                        pooledSuccesses++;
                    }
                }

                Console.WriteLine($"    ✔ Pooled completed: {pooledSuccesses}/{totalJobs} succeeded in {swPooled.ElapsedMilliseconds} ms.");
                Console.WriteLine("      (Notice: Reuses single socket safely; SemaphoreSlim guarantees zero byte interleaving)\n");
            }

            // 3. Demonstrating Cooperative Cancellation
            Console.WriteLine("[3] Demonstrating Cooperative Cancellation with CancellationToken:");
            using (var cancellationCts = new CancellationTokenSource())
            {
                cancellationCts.Cancel(); // Pre-canceled token

                var client = new TcpPrinterClient(options);
                var result = await client.PrintAsync(testDocument, cancellationCts.Token);

                Console.WriteLine($"    Result IsFailure: {result.IsFailure}");
                Console.WriteLine($"    Error Code: {result.Error.Code}");
                Console.WriteLine($"    Description: {result.Error.Description}");
                Console.WriteLine("    ✔ PrintAsync aborted immediately without network transmission.");
            }
        }
        finally
        {
            listenerCts.Cancel();
            listener.Stop();
            await Task.WhenAny(serverTask, Task.Delay(200));
        }

        Console.WriteLine("\n✔ Level 05 completed successfully.\n");
    }
}
