// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EricksonLopez.Printing.Serial;

namespace EricksonLopez.Printing;

/// <summary>
/// Provides extension methods for registering serial printing services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class PrintingSerialServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="SerialPrinterClient"/> as a <see cref="ServiceLifetime.Singleton"/>
    /// <see cref="IPrinterClient"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="configure">The delegate used to configure <see cref="SerialPrinterClientOptions"/>.</param>
    /// <returns>The service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddSerialPrinter(
        this IServiceCollection services,
        Action<SerialPrinterClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SerialPrinterClientOptions();
        configure(options);

        services.AddSingleton<IPrinterClient>(sp =>
            new SerialPrinterClient(options, sp.GetService<ILogger<SerialPrinterClient>>()));
        return services;
    }

    /// <summary>
    /// Registers a keyed <see cref="SerialPrinterClient"/> as a <see cref="ServiceLifetime.Singleton"/>
    /// <see cref="IPrinterClient"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="serviceKey">The key used to identify this printer client registration.</param>
    /// <param name="configure">The delegate used to configure <see cref="SerialPrinterClientOptions"/>.</param>
    /// <returns>The service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="serviceKey"/>, or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddKeyedSerialPrinter(
        this IServiceCollection services,
        object? serviceKey,
        Action<SerialPrinterClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SerialPrinterClientOptions();
        configure(options);

        services.AddKeyedSingleton<IPrinterClient>(serviceKey, (sp, key) =>
            new SerialPrinterClient(options, sp.GetService<ILogger<SerialPrinterClient>>()));
        return services;
    }
}

