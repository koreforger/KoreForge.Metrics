using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace KoreForge.Metrics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKoreForgeMetrics(this IServiceCollection services, Action<MonitoringOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MonitoringOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        services.TryAddSingleton<MonitoringEngine>();
        services.TryAddSingleton<IMonitoringDataSource>(sp => sp.GetRequiredService<MonitoringEngine>());
        services.TryAddSingleton<IOperationMonitor>(sp =>
        {
            var engine = sp.GetRequiredService<MonitoringEngine>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MonitoringOptions>>();
            return new OperationMonitor(engine, timeProvider, optionsMonitor);
        });
        services.TryAddSingleton<IMonitoringSnapshotProvider>(sp =>
        {
            var dataSource = sp.GetRequiredService<IMonitoringDataSource>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            return new MonitoringSnapshotProvider(dataSource, timeProvider);
        });

        return services;
    }
}
