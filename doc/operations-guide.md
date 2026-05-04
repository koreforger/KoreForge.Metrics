# Operations Playbook

Use this guide when you deploy KoreForge.Metrics and need to observe a live system.

## Expose a Snapshot Endpoint

1. Ensure `AddKoreForgeMetrics` is called during startup.
2. Choose one of the opt-in surfaces:
	 - **Minimal API**: `app.MapMonitoringEndpoints("/monitoring");`
	 - **Controller**: derive from `MonitoringControllerBase` and expose your own `[HttpGet]` action that calls `GetSnapshotCore()`.
3. Lock the route down (authentication/authorization) because snapshots may contain sensitive operation names and tags.

## Retrieve Data

- **Entire system**: `GET /monitoring/snapshot` returns `MonitoringSnapshot` with all operations.
- **Single operation**: call `IMonitoringSnapshotProvider.GetOperationSnapshot("name")` from diagnostic tooling or add a specific endpoint.
- **CPU percent**: look at `ProcessCpuPercent` inside `OperationCompletedContext` or expose the sampler history yourself.

## Interpret Snapshots

- `Operations[i].CurrentRatePerSecond` - average throughput over the hot window (`HotBucketCount x HotBucketSeconds`).
- `CurrentAverageDuration` / `CurrentMaxDuration` - derived from recent hot buckets and translated back to `TimeSpan`.
- `PerMinute` and `PerHour` - time-series arrays already ordered from oldest to newest; timestamps are aligned to the configured time mode.

## Monitor Event Pressure

- Drop counter: `MonitoringEngine.DroppedEvents` increases whenever the background queue is full and the dispatcher drops new events.
- Mitigations:
	1. Increase `EventQueueCapacity`.
	2. Reduce `SamplingRate` (higher value means fewer events) or disable low-priority sinks.
	3. Investigate sink latency; slow sinks should offload work asynchronously.

## CPU Sampling Checklist

1. Confirm `EnableCpuMeasurement = true` and redeploy.
2. Allow one or two intervals (`CpuSampleIntervalSeconds`) before expecting non-null CPU values.
3. Use the warm/cold windows to correlate CPU spikes with throughput changes.
4. Turn sampling back off if you do not actively need the data.

## Troubleshooting

- **No metrics for a name**: verify you have not exceeded `MaxOperationCount`. New names are ignored when the cap is hit.
- **Tags missing**: extra tags, or keys/values beyond configured limits, are trimmed silently per spec.
- **Stale snapshots**: ensure your diagnostics job resolves `IMonitoringSnapshotProvider` from the same DI container used to register the engine (avoid static caches).
- **High latency sinks**: switch to background queue mode (default) and rely on drops to protect callers.

