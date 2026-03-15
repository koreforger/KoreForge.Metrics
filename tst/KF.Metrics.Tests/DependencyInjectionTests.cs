using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KF.Metrics.Tests;

public class DependencyInjectionTests
{
	[Fact]
	public void AddMetrics_RegistersCoreServices()
	{
		var services = new ServiceCollection();
		services.AddKoreForgeMetrics();

		using var provider = services.BuildServiceProvider();
		var engine = provider.GetRequiredService<MonitoringEngine>();
		var dataSource = provider.GetRequiredService<IMonitoringDataSource>();
		var monitor = provider.GetRequiredService<IOperationMonitor>();
		var snapshots = provider.GetRequiredService<IMonitoringSnapshotProvider>();

		Assert.Same(engine, dataSource);
		Assert.NotNull(monitor);
		Assert.NotNull(snapshots);
	}

	[Fact]
	public void AddMetrics_AppliesConfiguration()
	{
		var services = new ServiceCollection();
		services.AddKoreForgeMetrics(options => options.MaxOperationCount = 42);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MonitoringOptions>>();

		Assert.Equal(42, options.Value.MaxOperationCount);
	}
}
