using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace KoreForge.Metrics;

public sealed class OperationMonitor : IOperationMonitor
{
    private readonly MonitoringEngine _engine;
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<MonitoringOptions> _optionsMonitor;

    public OperationMonitor(MonitoringEngine engine, TimeProvider timeProvider, IOptionsMonitor<MonitoringOptions> optionsMonitor)
    {
        _engine = engine;
        _timeProvider = timeProvider;
        _optionsMonitor = optionsMonitor;
    }

    public OperationScope Begin(string name, OperationTags? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var options = _optionsMonitor.CurrentValue;
        var metrics = _engine.GetOrCreateMetrics(name);
        if (metrics is null)
        {
            return OperationScope.CreateNoop();
        }

        metrics.RecordStart();
        var shouldSample = metrics.ShouldCaptureSample(options.SamplingRate);
        var sanitizedTags = shouldSample ? SanitizeTags(tags, options) : null;
        return new OperationScope(
            metrics,
            _engine,
            _timeProvider,
            name,
            sanitizedTags,
            shouldSample,
            _timeProvider.GetTimestamp());
    }

    private static IReadOnlyDictionary<string, string>? SanitizeTags(OperationTags? tags, MonitoringOptions options)
    {
        if (tags is null || tags.Count == 0 || options.MaxTagsPerOperation <= 0)
        {
            return null;
        }

        var result = new Dictionary<string, string>(options.MaxTagsPerOperation, StringComparer.Ordinal);
        foreach (var kvp in tags)
        {
            if (result.Count >= options.MaxTagsPerOperation)
            {
                break;
            }

            var key = Truncate(kvp.Key, options.MaxTagKeyLength);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var value = Truncate(kvp.Value, options.MaxTagValueLength);
            result[key] = value;
        }

        return result.Count == 0 ? null : result;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }
}

public sealed class OperationScope : IDisposable
{
    private readonly OperationMetrics? _metrics;
    private readonly MonitoringEngine _engine;
    private readonly TimeProvider _timeProvider;
    private readonly string _name;
    private readonly IReadOnlyDictionary<string, string>? _tags;
    private readonly bool _shouldSample;
    private readonly long _startTicks;
    private bool _failed;
    private bool _disposed;

    internal OperationScope(
        OperationMetrics? metrics,
        MonitoringEngine engine,
        TimeProvider timeProvider,
        string name,
        IReadOnlyDictionary<string, string>? tags,
        bool shouldSample,
        long startTicks)
    {
        _metrics = metrics;
        _engine = engine;
        _timeProvider = timeProvider;
        _name = name;
        _tags = tags;
        _shouldSample = shouldSample;
        _startTicks = startTicks;
    }

    private OperationScope()
    {
        _metrics = null;
        _engine = null!;
        _timeProvider = null!;
        _name = string.Empty;
        _shouldSample = false;
        _startTicks = 0;
    }

    internal static OperationScope CreateNoop() => new();

    public void MarkFailed()
    {
        if (_disposed || _metrics is null)
        {
            return;
        }

        _failed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_metrics is null)
        {
            return;
        }

        var durationTicks = _shouldSample ? Math.Max(0, _timeProvider.GetTimestamp() - _startTicks) : 0;
        var remaining = _metrics.RecordCompletion(durationTicks, _failed, _shouldSample);

        if (!_shouldSample)
        {
            return;
        }

        var context = new OperationCompletedContext
        {
            Name = _name,
            Duration = StopwatchTime.ToTimeSpan(durationTicks),
            IsFailure = _failed,
            ConcurrencyAtEnd = remaining,
            Tags = _tags,
            EndTime = _timeProvider.GetUtcNow(),
            ProcessCpuPercent = _engine.TryGetCpuPercent()
        };

        _engine.PublishEvent(context);
    }
}
