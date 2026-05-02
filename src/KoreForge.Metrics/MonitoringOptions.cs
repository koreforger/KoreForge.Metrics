using System;

namespace KoreForge.Metrics;

public sealed class MonitoringOptions
{
    public MonitoringTimeMode TimeMode { get; set; } = MonitoringTimeMode.Utc;

    public int HotBucketCount { get; set; } = 120;
    public int HotBucketSeconds { get; set; } = 1;

    public int WarmBucketCount { get; set; } = 60;
    public int WarmBucketMinutes { get; set; } = 1;

    public int ColdBucketCount { get; set; } = 24;
    public int ColdBucketHours { get; set; } = 1;

    public int SamplingRate { get; set; } = 1;

    public bool EnableCpuMeasurement { get; set; }
    public int CpuSampleIntervalSeconds { get; set; } = 1;
    public int CpuSampleHistoryCount { get; set; } = 120;

    public int MaxOperationCount { get; set; } = 500;
    public OperationOverflowPolicy OverflowPolicy { get; set; } = OperationOverflowPolicy.DropNew;

    public int MaxTagsPerOperation { get; set; } = 8;
    public int MaxTagKeyLength { get; set; } = 32;
    public int MaxTagValueLength { get; set; } = 64;

    public EventDispatchMode EventDispatchMode { get; set; } = EventDispatchMode.BackgroundQueue;
    public int EventQueueCapacity { get; set; } = 8192;
    public EventDropPolicy EventDropPolicy { get; set; } = EventDropPolicy.DropNew;
    public int EventDropLogThrottleSeconds { get; set; } = 60;

    public void Validate()
    {
        EnsurePositive(HotBucketCount, nameof(HotBucketCount));
        EnsurePositive(HotBucketSeconds, nameof(HotBucketSeconds));
        EnsurePositive(WarmBucketCount, nameof(WarmBucketCount));
        EnsurePositive(WarmBucketMinutes, nameof(WarmBucketMinutes));
        EnsurePositive(ColdBucketCount, nameof(ColdBucketCount));
        EnsurePositive(ColdBucketHours, nameof(ColdBucketHours));
        EnsurePositive(SamplingRate, nameof(SamplingRate));
        EnsurePositive(CpuSampleIntervalSeconds, nameof(CpuSampleIntervalSeconds));
        EnsurePositive(CpuSampleHistoryCount, nameof(CpuSampleHistoryCount));
        EnsurePositive(MaxOperationCount, nameof(MaxOperationCount));
        EnsurePositive(MaxTagsPerOperation, nameof(MaxTagsPerOperation));
        EnsurePositive(MaxTagKeyLength, nameof(MaxTagKeyLength));
        EnsurePositive(MaxTagValueLength, nameof(MaxTagValueLength));
        EnsurePositive(EventQueueCapacity, nameof(EventQueueCapacity));
        EnsurePositive(EventDropLogThrottleSeconds, nameof(EventDropLogThrottleSeconds));
    }

    private static void EnsurePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public enum MonitoringTimeMode
{
    Utc,
    Local
}

public enum EventDispatchMode
{
    Inline,
    BackgroundQueue
}

public enum EventDropPolicy
{
    DropNew
}

public enum OperationOverflowPolicy
{
    DropNew
}
