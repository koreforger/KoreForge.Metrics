using System;
using BenchmarkDotNet.Attributes;
using KF.Metrics;
using Microsoft.Extensions.Options;

namespace KF.Metrics.Benchmarks;

[MemoryDiagnoser]
public class OperationMonitorBenchmarks : IDisposable
{
	private readonly BenchmarkClock _clock = new();
	private readonly MonitoringEngine _inlineEngine;
	private readonly OperationMonitor _inlineMonitor;
	private readonly MonitoringEngine _queueEngine;
	private readonly OperationMonitor _queueMonitor;
	private readonly MonitoringEngine _sampleEngine;
	private readonly OperationMonitor _sampleMonitor;

	public OperationMonitorBenchmarks()
	{
		(_inlineEngine, _inlineMonitor) = CreateMonitor(new MonitoringOptions
		{
			EventDispatchMode = EventDispatchMode.Inline
		});

		(_queueEngine, _queueMonitor) = CreateMonitor(new MonitoringOptions
		{
			EventDispatchMode = EventDispatchMode.BackgroundQueue,
			EventQueueCapacity = 8192
		});

		(_sampleEngine, _sampleMonitor) = CreateMonitor(new MonitoringOptions
		{
			SamplingRate = 5,
			EventDispatchMode = EventDispatchMode.Inline
		});
	}

	[Benchmark(Baseline = true)]
	public void BeginDisposeInline()
	{
		var scope = _inlineMonitor.Begin("inline");
		_clock.Advance(TimeSpan.FromMilliseconds(1));
		scope.Dispose();
	}

	[Benchmark]
	public void BeginDisposeBackgroundQueue()
	{
		var scope = _queueMonitor.Begin("queue");
		_clock.Advance(TimeSpan.FromMilliseconds(1));
		scope.Dispose();
	}

	[Benchmark]
	public void BeginDisposeSampled()
	{
		var scope = _sampleMonitor.Begin("sampled");
		_clock.Advance(TimeSpan.FromMilliseconds(1));
		scope.Dispose();
	}

	public void Dispose()
	{
		_inlineEngine.Dispose();
		_queueEngine.Dispose();
		_sampleEngine.Dispose();
	}

	private (MonitoringEngine Engine, OperationMonitor Monitor) CreateMonitor(MonitoringOptions options)
	{
		var monitor = new FixedOptionsMonitor(options);
		var engine = new MonitoringEngine(monitor, Array.Empty<IOperationEventSink>());
		var operationMonitor = new OperationMonitor(engine, _clock, monitor);
		return (engine, operationMonitor);
	}

	private sealed class BenchmarkClock : TimeProvider
	{
		private long _ticks;

		public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

		public override long GetTimestamp() => _ticks;

		public void Advance(TimeSpan amount)
		{
			_ticks += (long)(amount.TotalSeconds * global::System.Diagnostics.Stopwatch.Frequency);
		}
	}

	private sealed class FixedOptionsMonitor : IOptionsMonitor<MonitoringOptions>
	{
		private readonly MonitoringOptions _options;

		public FixedOptionsMonitor(MonitoringOptions options) => _options = options;

		public MonitoringOptions CurrentValue => _options;

		public MonitoringOptions Get(string? name) => _options;

		public IDisposable OnChange(Action<MonitoringOptions, string> listener) => new NoopDisposable();

		private sealed class NoopDisposable : IDisposable
		{
			public void Dispose()
			{
			}
		}
	}
}
