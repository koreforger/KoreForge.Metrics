using System;
using System.Threading.Tasks;
using KoreForge.Metrics.Tests.Infrastructure;

namespace KoreForge.Metrics.Tests;

public class CpuMeasurementTests
{
	[Fact]
	public async Task ProcessCpuPercentIsCapturedWhenEnabled()
	{
		var options = new MonitoringOptions
		{
			EnableCpuMeasurement = true,
			CpuSampleIntervalSeconds = 1,
			EventDispatchMode = EventDispatchMode.Inline
		};

		OperationCompletedContext? recorded = null;
		var sink = new TestEventSink(ctx => recorded = ctx);
		using var engine = new MonitoringEngine(new TestOptionsMonitor(options), new[] { sink });
		var clock = new TestClock(DateTimeOffset.UtcNow);
		var monitor = new OperationMonitor(engine, clock, new TestOptionsMonitor(options));

		await Task.Delay(2100); // allow sampler to collect at least one sample

		var scope = monitor.Begin("cpu");
		clock.Advance(TimeSpan.FromMilliseconds(5));
		scope.Dispose();

		Assert.NotNull(recorded);
		Assert.True(recorded!.ProcessCpuPercent.HasValue);
	}
}
