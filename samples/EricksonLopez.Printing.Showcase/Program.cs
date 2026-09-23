// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EricksonLopez.Printing.Showcase.Levels;

namespace EricksonLopez.Printing.Showcase;

/// <summary>
/// Provides the entry point for the executable showcase demonstrating the EricksonLopez.Printing suite.
/// </summary>
public static class Program
{
    /// <summary>
    /// Executes the showcase application asynchronously.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>A task representing the asynchronous execution, returning 0 on success.</returns>
    public static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════════════╗
║               ERICKSONLOPEZ.PRINTING — OFFICIAL SHOWCASE                         ║
║               Reference Implementation & Executable Documentation                ║
╚══════════════════════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();

        var filter = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            switch (filter)
            {
                case "0":
                case "level0":
                case "level00":
                case "conceptual":
                    await Level00Conceptual.RunAsync();
                    break;

                case "1":
                case "level1":
                case "level01":
                case "quickstart":
                    await Level01QuickStart.RunAsync();
                    break;

                case "2":
                case "level2":
                case "level02":
                case "config":
                case "configuration":
                    await Level02FullConfiguration.RunAsync();
                    break;

                case "3":
                case "level3":
                case "level03":
                case "usecases":
                case "realworld":
                    await Level03RealWorldUseCases.RunAsync();
                    break;

                case "4":
                case "level4":
                case "level04":
                case "integration":
                case "imaging":
                    await Level04AdvancedIntegration.RunAsync();
                    break;

                case "5":
                case "level5":
                case "level05":
                case "concurrency":
                case "processing":
                    await Level05ProcessingAndConcurrency.RunAsync();
                    break;

                case "6":
                case "level6":
                case "level06":
                case "errors":
                case "resilience":
                    await Level06ErrorHandlingAndClassification.RunAsync();
                    break;

                case "7":
                case "level7":
                case "level07":
                case "scalability":
                case "throughput":
                    await Level07ScalabilityAndThroughput.RunAsync();
                    break;

                case "8":
                case "level8":
                case "level08":
                case "customization":
                case "extensibility":
                    await Level08CustomizationAndExtensibility.RunAsync();
                    break;

                case "9":
                case "level9":
                case "level09":
                case "signalr":
                case "dispatch":
                    await Level09RemoteDispatchSignalR.RunAsync();
                    break;

                case "10":
                case "level10":
                case "enterprise":
                case "status":
                    await Level10EnterpriseArchitecture.RunAsync();
                    break;

                case "all":
                default:
                    await RunAllLevelsAsync();
                    break;
            }

            totalStopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n================================================================================");
            Console.WriteLine($"  SUMMARY: All requested levels executed successfully.");
            Console.WriteLine($"  Total Showcase execution time: {totalStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine("================================================================================\n");
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL] Error in Showcase execution: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task RunAllLevelsAsync()
    {
        await Level00Conceptual.RunAsync();
        await Level01QuickStart.RunAsync();
        await Level02FullConfiguration.RunAsync();
        await Level03RealWorldUseCases.RunAsync();
        await Level04AdvancedIntegration.RunAsync();
        await Level05ProcessingAndConcurrency.RunAsync();
        await Level06ErrorHandlingAndClassification.RunAsync();
        await Level07ScalabilityAndThroughput.RunAsync();
        await Level08CustomizationAndExtensibility.RunAsync();
        await Level09RemoteDispatchSignalR.RunAsync();
        await Level10EnterpriseArchitecture.RunAsync();
    }
}
