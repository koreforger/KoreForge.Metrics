using System;
using System.Threading;
using System.Threading.Tasks;
using KF.Metrics.Tests.Infrastructure;

namespace KF.Metrics.Tests;

public class EventDispatchTests
{
	[Fact]
	public void InlineDispatchRunsOnCallerThread()
	{
		var options = new MonitoringOptions
		{
			EventDispatchMode = EventDispatchMode.Inline
		};

		var recordedThread = -1;
		var sink = new TestEventSink(_ => recordedThread = Environment.CurrentManagedThreadId);
		using var engine = new MonitoringEngine(new TestOptionsMonitor(options), new[] { sink });
		var clock = new TestClock(DateTimeOffset.UtcNow);
		var monitor = new OperationMonitor(engine, clock, new TestOptionsMonitor(options));

		var scope = monitor.Begin("inline");
		clock.Advance(TimeSpan.FromMilliseconds(1));
		var disposingThread = Environment.CurrentManagedThreadId;
		scope.Dispose();

		Assert.Equal(disposingThread, recordedThread);
	}

	[Fact]
	public async Task BackgroundQueueDropsWhenFull()
	{
		var options = new MonitoringOptions
		{
			EventDispatchMode = EventDispatchMode.BackgroundQueue,
			EventQueueCapacity = 1,
			EventDropPolicy = EventDropPolicy.DropNew
		};

		var blocker = new ManualResetEventSlim(false);
		var sink = new BlockingSink(blocker);
		using var engine = new MonitoringEngine(new TestOptionsMonitor(options), new[] { sink });
		var clock = new TestClock(DateTimeOffset.UtcNow);
		var monitor = new OperationMonitor(engine, clock, new TestOptionsMonitor(options));

		var scope = monitor.Begin("bg");
		clock.Advance(TimeSpan.FromMilliseconds(1));
		scope.Dispose();

		for (var i = 0; i < 1000; i++)
		{
			var s = monitor.Begin("bg");
			clock.Advance(TimeSpan.FromMilliseconds(1));
			s.Dispose();
		}

		// Give writer time to notice drops
		Assert.True(SpinWait.SpinUntil(() => engine.DroppedEvents > 0, TimeSpan.FromSeconds(2)));

		blocker.Set();
		await Task.Delay(100);
		Assert.True(sink.Processed <= 2);
	}

	private sealed class BlockingSink : IOperationEventSink
	{
		private readonly ManualResetEventSlim _blocker;

		public int Processed { get; private set; }

		public BlockingSink(ManualResetEventSlim blocker)
		{
			_blocker = blocker;
		}

		public void OnOperationCompleted(OperationCompletedContext context)
		{
			if (Processed == 0)
			{
				if (!_blocker.Wait(TimeSpan.FromSeconds(5)))
				{
					return;
				}
			}

			Processed++;
		}
	}
}
