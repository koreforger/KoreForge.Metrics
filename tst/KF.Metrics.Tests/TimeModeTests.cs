using System;
using KF.Metrics.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Metrics.Tests;

public class TimeModeTests
{
	[Fact]
	public void ServiceCollectionRegistersTimeProvider()
	{
		var services = new ServiceCollection();
		services.AddKoreForgeMetrics();

		var provider = services.BuildServiceProvider();
		var timeProvider = provider.GetRequiredService<TimeProvider>();

		Assert.Same(TimeProvider.System, timeProvider);
	}

	[Fact]
	public void SnapshotUsesClockTimestamp()
	{
		var options = new MonitoringOptions
		{
			EventDispatchMode = EventDispatchMode.Inline
		};

		using var engine = new MonitoringEngine(new TestOptionsMonitor(options), Array.Empty<IOperationEventSink>());
		var clock = new TestClock(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
		var provider = new MonitoringSnapshotProvider(engine, clock);

		var snapshot = provider.GetSnapshot();

		Assert.Equal(clock.GetUtcNow(), snapshot.GeneratedAt);
	}
}
