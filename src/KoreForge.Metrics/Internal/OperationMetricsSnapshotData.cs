namespace KoreForge.Metrics;

internal sealed record OperationMetricsSnapshotData(
    string Name,
    long TotalCount,
    long TotalFailures,
    int CurrentInFlight,
    int PeakInFlight,
    MetricsBucket[] HotBuckets,
    int HotBucketIndex,
    MetricsBucket[] WarmBuckets,
    int WarmBucketIndex,
    MetricsBucket[] ColdBuckets,
    int ColdBucketIndex);
