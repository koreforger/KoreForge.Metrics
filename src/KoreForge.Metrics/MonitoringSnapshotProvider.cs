using System;
using System.Collections.Generic;
using System.Linq;

namespace KoreForge.Metrics;

public sealed class MonitoringSnapshotProvider : IMonitoringSnapshotProvider
{
    private readonly IMonitoringDataSource _dataSource;
    private readonly TimeProvider _timeProvider;

    internal MonitoringSnapshotProvider(IMonitoringDataSource dataSource, TimeProvider timeProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public MonitoringSnapshot GetSnapshot()
    {
        var generatedAt = _timeProvider.GetUtcNow();
        var options = _dataSource.CurrentOptions;
        var operations = _dataSource
            .CaptureSnapshots()
            .Select(data => CreateOperationSnapshot(data, options, generatedAt))
            .OrderBy(op => op.Name, StringComparer.Ordinal)
            .ToArray();

        return new MonitoringSnapshot(generatedAt, operations);
    }

    public OperationSnapshot? GetOperationSnapshot(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var snapshot = _dataSource
            .CaptureSnapshots()
            .FirstOrDefault(data => string.Equals(data.Name, name, StringComparison.Ordinal));

        if (snapshot is null)
        {
            return null;
        }

        return CreateOperationSnapshot(snapshot, _dataSource.CurrentOptions, _timeProvider.GetUtcNow());
    }

    private static OperationSnapshot CreateOperationSnapshot(
        OperationMetricsSnapshotData data,
        MonitoringOptions options,
        DateTimeOffset generatedAt)
    {
        var (hotCount, averageDuration, maxDuration) = AggregateHotBuckets(data.HotBuckets);
        var perMinute = BuildSeries(
            data.WarmBuckets,
            data.WarmBucketIndex,
            TimeSpan.FromMinutes(options.WarmBucketMinutes),
            generatedAt);
        var perHour = BuildSeries(
            data.ColdBuckets,
            data.ColdBucketIndex,
            TimeSpan.FromHours(options.ColdBucketHours),
            generatedAt);

        return new OperationSnapshot(
            data.Name,
            data.TotalCount,
            data.TotalFailures,
            data.CurrentInFlight,
            data.PeakInFlight,
            CalculateRatePerSecond(hotCount, options),
            averageDuration,
            maxDuration,
            perMinute,
            perHour);
    }

    private static (long Count, TimeSpan Average, TimeSpan Max) AggregateHotBuckets(IReadOnlyList<MetricsBucket> buckets)
    {
        long totalCount = 0;
        long totalTicks = 0;
        long maxTicks = 0;

        foreach (var bucket in buckets)
        {
            totalCount += bucket.Count;
            totalTicks += bucket.TotalTicks;
            if (bucket.MaxTicks > maxTicks)
            {
                maxTicks = bucket.MaxTicks;
            }
        }

        var average = totalCount > 0
            ? StopwatchTime.ToTimeSpan(totalTicks / (double)totalCount)
            : TimeSpan.Zero;
        var max = StopwatchTime.ToTimeSpan(maxTicks);
        return (totalCount, average, max);
    }

    private static IReadOnlyList<TimeSeriesPoint> BuildSeries(
        IReadOnlyList<MetricsBucket> buckets,
        int currentIndex,
        TimeSpan bucketDuration,
        DateTimeOffset now)
    {
        if (buckets.Count == 0 || bucketDuration <= TimeSpan.Zero)
        {
            return Array.Empty<TimeSeriesPoint>();
        }

        var ordered = new TimeSeriesPoint[buckets.Count];
        var oldestOffset = bucketDuration * (buckets.Count - 1);
        var firstTimestamp = now - oldestOffset;

        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[(currentIndex + 1 + i) % buckets.Count];
            var timestamp = firstTimestamp + bucketDuration * i;
            ordered[i] = new TimeSeriesPoint(
                timestamp,
                bucket.Count,
                CalculateAverage(bucket),
                CalculateMax(bucket));
        }

        return ordered;
    }

    private static TimeSpan CalculateAverage(MetricsBucket bucket)
    {
        return bucket.Count > 0
            ? StopwatchTime.ToTimeSpan(bucket.TotalTicks / (double)bucket.Count)
            : TimeSpan.Zero;
    }

    private static TimeSpan CalculateMax(MetricsBucket bucket)
    {
        return bucket.Count > 0
            ? StopwatchTime.ToTimeSpan(bucket.MaxTicks)
            : TimeSpan.Zero;
    }

    private static double CalculateRatePerSecond(long count, MonitoringOptions options)
    {
        var windowSeconds = (long)options.HotBucketCount * options.HotBucketSeconds;
        if (windowSeconds <= 0)
        {
            return 0d;
        }

        return count / (double)windowSeconds;
    }
}
