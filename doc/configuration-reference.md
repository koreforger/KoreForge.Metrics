# Configuration Reference

Use `MonitoringOptions` (surfaced via `AddKoreForgeMetrics`) to tune behavior. The defaults follow the v1 specification and are summarized below.

## Time & Sampling

| Option      | Default | Notes                                                                 |
|-------------|---------|------------------------------------------------------------------------|
| `TimeMode`  | `Utc`   | Switch to `Local` if you want timestamps to reflect server local time. |
| `SamplingRate` | `1`  | Every request publishes events; set to `N > 1` to reduce event volume. |

## Buckets & Retention

| Option                              | Default     | Effect                                             |
|-------------------------------------|-------------|----------------------------------------------------|
| `HotBucketCount` / `HotBucketSeconds` | 120 x 1 s | ~2 minutes at 1-second resolution (rate/latency).   |
| `WarmBucketCount` / `WarmBucketMinutes` | 60 x 1 min | ~1 hour of per-minute history.                     |
| `ColdBucketCount` / `ColdBucketHours` | 24 x 1 hr  | ~24 hours of per-hour history.                     |

All windows are fixed-size rings; increasing counts raises memory usage linearly.

## Operation Inventory

| Option             | Default | Notes                                                       |
|--------------------|---------|-------------------------------------------------------------|
| `MaxOperationCount`| 500     | Caps the number of distinct operation names tracked.        |
| `OverflowPolicy`   | `DropNew` | New operation names are ignored once the cap is reached. |

## Tags

| Option              | Default | Notes                                   |
|---------------------|---------|-----------------------------------------|
| `MaxTagsPerOperation` | 8     | Extra tags are silently ignored.        |
| `MaxTagKeyLength`   | 32      | Keys longer than the limit are truncated.|
| `MaxTagValueLength` | 64      | Values longer than the limit are truncated.|

## Event Dispatch

| Option                     | Default           | Notes                                                                 |
|----------------------------|-------------------|-----------------------------------------------------------------------|
| `EventDispatchMode`        | `BackgroundQueue` | Use `Inline` for very low volume scenarios that require immediate sinks.|
| `EventQueueCapacity`       | 8192              | Capacity of the bounded background queue.                             |
| `EventDropPolicy`          | `DropNew`         | Current behavior drops the newest event when the queue is full.       |
| `EventDropLogThrottleSeconds` | 60            | Minimum gap between drop warnings.                                    |

## CPU Sampling

| Option                    | Default | Notes                                                           |
|---------------------------|---------|-----------------------------------------------------------------|
| `EnableCpuMeasurement`    | `false` | When off, `ProcessCpuPercent` is `null`.                         |
| `CpuSampleIntervalSeconds`| 1       | How often to poll `Process.TotalProcessorTime`.                  |
| `CpuSampleHistoryCount`   | 120     | Length of the ring buffer holding historical CPU samples.       |

## Recommended Tuning Steps

1. Start with defaults and observe throughput.
2. Increase `EventQueueCapacity` or raise `SamplingRate` if `DroppedEvents` climbs.
3. Lower `MaxOperationCount` if you only care about a small whitelist of names.
4. Enable CPU sampling temporarily during investigations to minimize background overhead.
