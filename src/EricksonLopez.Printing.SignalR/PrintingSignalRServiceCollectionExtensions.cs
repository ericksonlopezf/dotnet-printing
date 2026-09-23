// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Provides extension methods for registering SignalR printing services in Microsoft Dependency Injection.
/// </summary>
public static class PrintingSignalRServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IHubPrinterDispatcher"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPrintingSignalR(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSignalR();
        services.AddSingleton<IHubPrinterDispatcher, HubPrinterDispatcher>();
        return services;
    }
}
