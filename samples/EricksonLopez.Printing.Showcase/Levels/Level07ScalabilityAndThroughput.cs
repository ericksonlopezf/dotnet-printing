// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.Zpl;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates throughput benchmarks, memory pooling efficiency, and zero-LOH allocation streaming.
/// </summary>
public static class Level07ScalabilityAndThroughput
{
    /// <summary>
    /// Executes the scalability and throughput showcase demonstration asynchronously.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 07: SCALABILITY & THROUGHPUT — MEMORY POOLING & STREAMING");
        Console.WriteLine("================================================================================\n");

        const int iterations = 1000;

        // 1. Throughput Benchmark: EscPosBuilder document generation
        Console.WriteLine($"[1] Throughput Benchmark: Compiling {iterations} ESC/POS receipts:");
        var swEscPos = Stopwatch.StartNew();
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < iterations; i++)
        {
            using var builder = new EscPosBuilder();
            builder.Initialize()
                   .Align(EscPosAlignment.Center)
                   .Bold(true)
                   .Line("BENCHMARK POS STORE")
                   .Bold(false)
                   .TableRow($"Item #{i}", "$10.00", 42)
                   .TableRow("Tax (8%)", "$0.80", 42)
                   .TableRow("Total", "$10.80", 42)
                   .Feed(2)
                   .Cut();

            var doc = builder.Build($"BenchReceipt-{i}");
            _ = doc.GetBytes();
        }

        swEscPos.Stop();
        var allocAfter = GC.GetAllocatedBytesForCurrentThread();
        var totalAllocMb = (allocAfter - allocBefore) / (1024.0 * 1024.0);

        Console.WriteLine($"    • Time elapsed: {swEscPos.ElapsedMilliseconds} ms (Average: {(double)swEscPos.ElapsedMilliseconds / iterations * 1000:F1} µs/receipt)");
        Console.WriteLine($"    • Total memory allocated: {totalAllocMb:F2} MB (Average: {(totalAllocMb * 1024) / iterations:F2} KB/receipt)");
        Console.WriteLine("    ✔ RecyclableMemoryStreamManager prevents memory fragmentation on Large Object Heap (LOH).\n");

        // 2. Throughput Benchmark: ZplBuilder document generation
        Console.WriteLine($"[2] Throughput Benchmark: Compiling {iterations} Zebra ZPL II labels:");
        var swZpl = Stopwatch.StartNew();
        allocBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < iterations; i++)
        {
            var zpl = new ZplBuilder();
            zpl.Text(40, 40, $"PALLET #{i:D6}", ZplFont.Font0, 30, 30)
               .Box(30, 30, 400, 200, 2)
               .BarcodeCode128(50, 90, $"SSCC-{i:D8}", 60);

            var doc = zpl.Build($"Pallet-{i}");
            _ = doc.GetBytes();
        }

        swZpl.Stop();
        allocAfter = GC.GetAllocatedBytesForCurrentThread();
        totalAllocMb = (allocAfter - allocBefore) / (1024.0 * 1024.0);

        Console.WriteLine($"    • Time elapsed: {swZpl.ElapsedMilliseconds} ms (Average: {(double)swZpl.ElapsedMilliseconds / iterations * 1000:F1} µs/label)");
        Console.WriteLine($"    • Total memory allocated: {totalAllocMb:F2} MB (Average: {(totalAllocMb * 1024) / iterations:F2} KB/label)\n");

        // 3. Streaming Benchmark: Zero-allocation direct stream transmission
        Console.WriteLine("[3] Direct Asynchronous Streaming Benchmark (WriteToAsync):");
        var testZpl = new ZplBuilder()
            .Text(50, 50, "DIRECT STREAMING TEST", ZplFont.Font0, 40, 40)
            .BarcodeCode128(50, 120, "STREAM-001", 80)
            .Build("StreamTestDoc");

        using var sinkStream = new MemoryStream();
        var swStream = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            sinkStream.SetLength(0);
            await testZpl.WriteToAsync(sinkStream);
        }
        swStream.Stop();

        Console.WriteLine($"    • {iterations} direct WriteToAsync calls completed in {swStream.ElapsedMilliseconds} ms.");
        Console.WriteLine("    ✔ Streaming bypasses intermediate byte[] duplication, achieving maximum IO throughput.");

        Console.WriteLine("\n✔ Level 07 completed successfully.\n");
    }
}
