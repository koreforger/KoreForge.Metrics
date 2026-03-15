using System;
using System.Linq;
using System.Threading.Tasks;
using KF.Metrics.Tests.Infrastructure;

namespace KF.Metrics.Tests;

public class ConcurrencyTests
{
	[Fact]
	public async Task MetricsRemainConsistentUnderConcurrency()
	{
		var options = new MonitoringOptions
		{
			EventDispatchMode = EventDispatchMode.Inline
		};

		using var engine = new MonitoringEngine(new TestOptionsMonitor(options), Array.Empty<IOperationEventSink>());
		var monitor = new OperationMonitor(engine, TimeProvider.System, new TestOptionsMonitor(options));

		var tasks = Enumerable.Range(0, 1000).Select(async _ =>
		{
			var scope = monitor.Begin("concurrent");
			await Task.Yield();
			scope.Dispose();
		});

		await Task.WhenAll(tasks);
		var metrics = engine.TryGetMetrics("concurrent")!;
		var snapshot = metrics.CaptureSnapshot();
		Assert.Equal(1000, snapshot.TotalCount);
		Assert.Equal(0, snapshot.CurrentInFlight);
		Assert.True(snapshot.PeakInFlight > 0);
	}
}
