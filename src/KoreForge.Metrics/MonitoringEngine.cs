using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Options;

namespace KoreForge.Metrics;

public sealed class MonitoringEngine : IMonitoringDataSource, IDisposable
{
    private readonly IOptionsMonitor<MonitoringOptions> _optionsMonitor;
    private readonly IDisposable _optionsSubscription;
    private readonly Dictionary<string, OperationMetrics> _metrics = new(StringComparer.Ordinal);
    private readonly object _metricsGate = new();
    private readonly EventDispatcher _dispatcher;
    private Timer? _hotTimer;
    private Timer? _warmTimer;
    private Timer? _coldTimer;
    private MonitoringOptions _currentOptions;
    private ProcessCpuSampler? _cpuSampler;
    private bool _disposed;
    private readonly TimeProvider _timeProvider;

    public MonitoringEngine(
        IOptionsMonitor<MonitoringOptions> optionsMonitor,
        IEnumerable<IOperationEventSink> sinks,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(sinks);

        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dispatcher = new EventDispatcher(sinks);
        _optionsSubscription = optionsMonitor.OnChange(options => ApplyOptions(CloneOptions(options)))
            ?? throw new InvalidOperationException("Options monitor returned a null subscription.");
        _currentOptions = CloneOptions(optionsMonitor.CurrentValue);
        ApplyOptions(_currentOptions);
    }

    public MonitoringOptions CurrentOptions => _currentOptions;

    public long DroppedEvents => _dispatcher.DroppedEvents;

    internal IEnumerable<OperationMetricsSnapshotData> CaptureSnapshots()
    {
        OperationMetrics[] snapshot;
        lock (_metricsGate)
        {
            snapshot = _metrics.Values.ToArray();
        }

        return snapshot.Select(m => m.CaptureSnapshot());
    }

    IEnumerable<OperationMetricsSnapshotData> IMonitoringDataSource.CaptureSnapshots() => CaptureSnapshots();

    internal OperationMetrics? GetOrCreateMetrics(string name)
    {
        var options = _currentOptions;
        lock (_metricsGate)
        {
            if (_metrics.TryGetValue(name, out var existing))
            {
                return existing;
            }

            if (_metrics.Count >= options.MaxOperationCount && options.OverflowPolicy == OperationOverflowPolicy.DropNew)
            {
                return null;
            }

            var created = new OperationMetrics(name, options);
            _metrics[name] = created;
            return created;
        }
    }

    internal OperationMetrics? TryGetMetrics(string name)
    {
        lock (_metricsGate)
        {
            return _metrics.TryGetValue(name, out var metrics) ? metrics : null;
        }
    }

    internal double? TryGetCpuPercent() => _cpuSampler?.LatestSample;

    internal void PublishEvent(OperationCompletedContext context) => _dispatcher.Publish(context);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _optionsSubscription.Dispose();
        _dispatcher.Dispose();
        StopTimers();
        _cpuSampler?.Dispose();
    }

    private void ApplyOptions(MonitoringOptions options)
    {
        options.Validate();
        _currentOptions = options;
        _dispatcher.UpdateOptions(options);
        ConfigureCpuSampler(options);
        RestartTimers(options);
    }

    private void ConfigureCpuSampler(MonitoringOptions options)
    {
        _cpuSampler?.Dispose();
        _cpuSampler = options.EnableCpuMeasurement ? new ProcessCpuSampler(options, _timeProvider) : null;
    }

    private void RestartTimers(MonitoringOptions options)
    {
        StopTimers();
        _hotTimer = new Timer(_ => AdvanceAll(static m => m.AdvanceHot()), null, TimeSpan.FromSeconds(options.HotBucketSeconds), TimeSpan.FromSeconds(options.HotBucketSeconds));
        _warmTimer = new Timer(_ => AdvanceAll(static m => m.AdvanceWarm()), null, TimeSpan.FromMinutes(options.WarmBucketMinutes), TimeSpan.FromMinutes(options.WarmBucketMinutes));
        _coldTimer = new Timer(_ => AdvanceAll(static m => m.AdvanceCold()), null, TimeSpan.FromHours(options.ColdBucketHours), TimeSpan.FromHours(options.ColdBucketHours));
    }

    private void StopTimers()
    {
        _hotTimer?.Dispose();
        _warmTimer?.Dispose();
        _coldTimer?.Dispose();
        _hotTimer = null;
        _warmTimer = null;
        _coldTimer = null;
    }

    private void AdvanceAll(Action<OperationMetrics> advance)
    {
        OperationMetrics[] snapshot;
        lock (_metricsGate)
        {
            snapshot = _metrics.Values.ToArray();
        }

        foreach (var metrics in snapshot)
        {
            advance(metrics);
        }
    }

    private static MonitoringOptions CloneOptions(MonitoringOptions source)
    {
        return new MonitoringOptions
        {
            TimeMode = source.TimeMode,
            HotBucketCount = source.HotBucketCount,
            HotBucketSeconds = source.HotBucketSeconds,
            WarmBucketCount = source.WarmBucketCount,
            WarmBucketMinutes = source.WarmBucketMinutes,
            ColdBucketCount = source.ColdBucketCount,
            ColdBucketHours = source.ColdBucketHours,
            SamplingRate = source.SamplingRate,
            EnableCpuMeasurement = source.EnableCpuMeasurement,
            CpuSampleIntervalSeconds = source.CpuSampleIntervalSeconds,
            CpuSampleHistoryCount = source.CpuSampleHistoryCount,
            MaxOperationCount = source.MaxOperationCount,
            OverflowPolicy = source.OverflowPolicy,
            MaxTagsPerOperation = source.MaxTagsPerOperation,
            MaxTagKeyLength = source.MaxTagKeyLength,
            MaxTagValueLength = source.MaxTagValueLength,
            EventDispatchMode = source.EventDispatchMode,
            EventQueueCapacity = source.EventQueueCapacity,
            EventDropPolicy = source.EventDropPolicy,
            EventDropLogThrottleSeconds = source.EventDropLogThrottleSeconds
        };
    }
}
