using System;
using System.Collections.Generic;

namespace KF.Metrics;

public sealed class MonitoringSnapshot
{
    public MonitoringSnapshot(DateTimeOffset generatedAt, IReadOnlyList<OperationSnapshot> operations)
    {
        GeneratedAt = generatedAt;
        Operations = operations;
    }

    public DateTimeOffset GeneratedAt { get; }

    public IReadOnlyList<OperationSnapshot> Operations { get; }
}

public sealed class OperationSnapshot
{
    public OperationSnapshot(
        string name,
        long totalCount,
        long totalFailures,
        int currentInFlight,
        int peakInFlight,
        double currentRatePerSecond,
        TimeSpan currentAverageDuration,
        TimeSpan currentMaxDuration,
        IReadOnlyList<TimeSeriesPoint> perMinute,
        IReadOnlyList<TimeSeriesPoint> perHour)
    {
        Name = name;
        TotalCount = totalCount;
        TotalFailures = totalFailures;
        CurrentInFlight = currentInFlight;
        PeakInFlight = peakInFlight;
        CurrentRatePerSecond = currentRatePerSecond;
        CurrentAverageDuration = currentAverageDuration;
        CurrentMaxDuration = currentMaxDuration;
        PerMinute = perMinute;
        PerHour = perHour;
    }

    public string Name { get; }

    public long TotalCount { get; }

    public long TotalFailures { get; }

    public int CurrentInFlight { get; }

    public int PeakInFlight { get; }

    public double CurrentRatePerSecond { get; }

    public TimeSpan CurrentAverageDuration { get; }

    public TimeSpan CurrentMaxDuration { get; }

    public IReadOnlyList<TimeSeriesPoint> PerMinute { get; }

    public IReadOnlyList<TimeSeriesPoint> PerHour { get; }
}

public readonly struct TimeSeriesPoint
{
    public TimeSeriesPoint(DateTimeOffset timestamp, long count, TimeSpan averageDuration, TimeSpan maxDuration)
    {
        Timestamp = timestamp;
        Count = count;
        AverageDuration = averageDuration;
        MaxDuration = maxDuration;
    }

    public DateTimeOffset Timestamp { get; }

    public long Count { get; }

    public TimeSpan AverageDuration { get; }

    public TimeSpan MaxDuration { get; }
}
