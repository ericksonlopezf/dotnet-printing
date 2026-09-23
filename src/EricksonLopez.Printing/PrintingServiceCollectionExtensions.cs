// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Printing;

/// <summary>
/// Provides extension methods for registering TCP printing services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class PrintingServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="TcpPrinterClient"/> as a <see cref="ServiceLifetime.Singleton"/>
    /// <see cref="IPrinterClient"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="configure">The delegate used to configure <see cref="TcpPrinterClientOptions"/>.</param>
    /// <returns>The service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddTcpPrinter(
        this IServiceCollection services,
        Action<TcpPrinterClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TcpPrinterClientOptions();
        configure(options);

        services.AddSingleton<IPrinterClient>(sp =>
            new TcpPrinterClient(options, sp.GetService<ILogger<TcpPrinterClient>>()));
        return services;
    }

    /// <summary>
    /// Registers a keyed <see cref="TcpPrinterClient"/> as a <see cref="ServiceLifetime.Singleton"/>
    /// <see cref="IPrinterClient"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="serviceKey">The key used to identify this printer client registration.</param>
    /// <param name="configure">The delegate used to configure <see cref="TcpPrinterClientOptions"/>.</param>
    /// <returns>The service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="serviceKey"/>, or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddKeyedTcpPrinter(
        this IServiceCollection services,
        object? serviceKey,
        Action<TcpPrinterClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TcpPrinterClientOptions();
        configure(options);

        services.AddKeyedSingleton<IPrinterClient>(serviceKey, (sp, key) =>
            new TcpPrinterClient(options, sp.GetService<ILogger<TcpPrinterClient>>()));
        return services;
    }

    /// <summary>
    /// Registers a persistent <see cref="PooledTcpPrinterClient"/> as a <see cref="ServiceLifetime.Singleton"/>
    /// <see cref="IPrinterClient"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="configure">The delegate used to configure <see cref="TcpPrinterClientOptions"/>.</param>
    /// <returns>The service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPooledTcpPrinter(
        this IServiceCollection services,
        Action<TcpPrinterClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TcpPrinterClientOptions();
        configure(options);

        services.AddSingleton<IPrinterClient>(sp =>
            new PooledTcpPrinterClient(options, sp.GetService<ILogger<PooledTcpPrinterClient>>()));
        return services;
    }

    /// <summary>
    /// Registers a keyed persistent <see cref="PooledTcpPrinterClient"/> as a <see cref="ServiceLifetime.Singleton"/>
    /// <see cref="IPrinterClient"/> in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the client into.</param>
    /// <param name="serviceKey">The key used to identify this printer client registration.</param>
    /// <param name="configure">The delegate used to configure <see cref="TcpPrinterClientOptions"/>.</param>
    /// <returns>The service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="serviceKey"/>, or <paramref name="configure"/> is <see langword="null"/></exception>
    public static IServiceCollection AddKeyedPooledTcpPrinter(
        this IServiceCollection services,
        object? serviceKey,
        Action<TcpPrinterClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TcpPrinterClientOptions();
        configure(options);

        services.AddKeyedSingleton<IPrinterClient>(serviceKey, (sp, key) =>
            new PooledTcpPrinterClient(options, sp.GetService<ILogger<PooledTcpPrinterClient>>()));
        return services;
    }
}
