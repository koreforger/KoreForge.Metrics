# KoreForge.Metrics - Final Design Specification (v1)

## 1. Scope

KoreForge.Metrics is a .NET library that:

- Measures **frequency** and **latency** of logical operations.
- Stores metrics in **bounded**, in-memory ring buffers.
- Provides **event hooks** for slow or hot operations.
- Optionally exposes metrics via ASP.NET endpoints (explicit opt-in).
- Uses a **global time mode** (UTC or Local) decided at startup.

This document defines:

- Configuration defaults and limits (exact values).
- Supported runtimes and dependencies.
- Event sink dispatch behavior.
- Snapshot shape, immutability, and serialization assumptions.
- CPU timing approach and acceptable overhead.
- Testing and benchmark expectations.

Everything below is intended to remove ambiguity.

---

## 2. Target Runtime & Dependencies

### 2.1 Target Frameworks

- **KoreForge.Metrics.Core** - target `net10.0`.
- **KoreForge.Metrics.AspNet** - target `net10.0`.
- Optional future packages (for example, OpenTelemetry integration) also target `net10.0`.
- No multi-targeting in v1.

### 2.2 Dependencies

- **Core**
  - Only `System.*` assemblies (BCL).
  - **No** direct dependency on: ASP.NET, `System.Diagnostics.Metrics`, OpenTelemetry, or any third-party packages.
- **KoreForge.Metrics.AspNet**
  - Depends on the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`).
- **Optional integrations** (separate packages, not part of v1 core):
  - `KoreForge.Metrics.OpenTelemetry` may depend on `OpenTelemetry`, `OpenTelemetry.Metrics`, or `System.Diagnostics.Metrics`.
- **Tests/Benchmarks**
  - May use xUnit/NUnit and `BenchmarkDotNet`.
  - These are test-only; they must not be referenced from runtime packages.

---

## 3. Configuration Options - Defaults & Limits

All configuration flows through `MonitoringOptions`.

### 3.1 Time Mode (UTC vs Local)

```csharp
public enum MonitoringTimeMode
{
    Utc,
    Local
}
```

In `MonitoringOptions`:

- `TimeMode` default: `MonitoringTimeMode.Utc`.
- Environments set `TimeMode = MonitoringTimeMode.Local` at startup when local timestamps are desired.

All timestamps exposed via public APIs (snapshots, events) must:

- Use `DateTimeOffset`.
- Be derived from `TimeMode` (either `DateTimeOffset.UtcNow` or `DateTimeOffset.Now`) via `ISystemClock`.

Internal timing must use `Stopwatch.GetTimestamp()` (time-zone independent).

### 3.2 Buckets & Windows

`MonitoringOptions` must expose:

```csharp
public int HotBucketCount { get; set; }
public int HotBucketSeconds { get; set; }
public int WarmBucketCount { get; set; }
public int WarmBucketMinutes { get; set; }
public int ColdBucketCount { get; set; }
public int ColdBucketHours { get; set; }
```

**Default values:**

- `HotBucketCount = 120`
- `HotBucketSeconds = 1` (~2 minutes of 1-second resolution)
- `WarmBucketCount = 60`
- `WarmBucketMinutes = 1` (~1 hour of 1-minute resolution)
- `ColdBucketCount = 24`
- `ColdBucketHours = 1` (~24 hours of 1-hour resolution)

**Constraints:**

- All counts must be > 0.
- All durations must be > 0.
- Buckets must be fixed-size ring buffers; no dynamic growth.

### 3.3 Sampling

```csharp
public int SamplingRate { get; set; }
```

**Default:** `SamplingRate = 1` (every call fully measured and evented).

**Behavior:**

- Use a per-operation or per-engine counter.
  - If `SamplingRate <= 1`, measure every call.
  - If `SamplingRate > 1`:
    - Always increment total counters.
    - Only perform full timing/event processing when `counter % SamplingRate == 0`.
- Sampling must not affect `TotalCount` and `TotalFailures`; it only affects detailed timing and events.

### 3.4 CPU Measurement

```csharp
public bool EnableCpuMeasurement { get; set; }
public int CpuSampleIntervalSeconds { get; set; }
public int CpuSampleHistoryCount { get; set; }
```

**Defaults:**

- `EnableCpuMeasurement = false`
- `CpuSampleIntervalSeconds = 1`
- `CpuSampleHistoryCount = 120` (last 2 minutes of CPU percentage samples)

**Behavior (v1):**

- If disabled: CPU-related fields in snapshots/events must be `null` or 0 as appropriate.
- If enabled:
  - A background sampler must:
    - Every `CpuSampleIntervalSeconds` seconds:
      - Call `Process.GetCurrentProcess().TotalProcessorTime`.
      - Compute CPU percent over the interval using:
        - Delta of `TotalProcessorTime`.
        - Delta of wall-clock time.
        - Number of logical processors (`Environment.ProcessorCount`).
      - Store CPU percent in a ring buffer of length `CpuSampleHistoryCount`.
  - `OperationCompletedContext.ProcessCpuPercent` must contain the latest sample (or `null` if not yet available).
- Per-operation CPU time is out of scope in v1.

### 3.5 Operation Count & Overflow Policy

```csharp
public int MaxOperationCount { get; set; }
public OperationOverflowPolicy OverflowPolicy { get; set; }

public enum OperationOverflowPolicy
{
    DropNew
}
```

**Defaults:**

- `MaxOperationCount = 500`
- `OverflowPolicy = OperationOverflowPolicy.DropNew`

**Behavior:**

- The engine must maintain at most `MaxOperationCount` distinct operation entries.
- When a new operation name arrives and the dictionary already has `MaxOperationCount` entries:
  - With `DropNew`:
    - The new operation must not be added.
    - No metrics must be recorded for that operation.
- The engine may log a warning the first time this occurs, and should throttle subsequent logs (for example once per minute).

### 3.6 Tag Limits

```csharp
public int MaxTagsPerOperation { get; set; }
public int MaxTagKeyLength { get; set; }
public int MaxTagValueLength { get; set; }
```

**Defaults:**

- `MaxTagsPerOperation = 8`
- `MaxTagKeyLength = 32`
- `MaxTagValueLength = 64`

**Behavior:**

- Attempts to attach more than `MaxTagsPerOperation` tags must cause extra tags to be ignored.
- Keys longer than `MaxTagKeyLength` must be truncated to that length.
- Values longer than `MaxTagValueLength` must be truncated to that length.
- Tag collections (`OperationTags`) must be immutable once created.

---

## 4. DI & Container Integration

### 4.1 Primary DI: IServiceCollection

The supported and optimized path is `Microsoft.Extensions.DependencyInjection`.

```csharp
public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddKoreForgeMetrics(
        this IServiceCollection services,
        Action<MonitoringOptions>? configure = null);
}
```

**Behavior:**

- Must register:
  - `IOptions<MonitoringOptions>` / `IOptionsMonitor<MonitoringOptions>`.
  - `ISystemClock` (honoring `TimeMode`).
  - The central metrics engine as a singleton.
  - `IOperationMonitor` as a singleton.
  - `IMonitoringSnapshotProvider` as a singleton.
  - Background timer / CPU sampler / event dispatch worker as needed.

### 4.2 Other Containers

- The library must not depend directly on other container abstractions.
- Other DI containers may:
  - Use `IServiceCollection` integration via adapters, or
  - Manually register the concrete dependencies documented above.
- No additional factory or registration helpers are required beyond `AddKoreForgeMetrics`.

---

## 5. Event Sinks & Dispatch Behavior

### 5.1 Event Sink Interface

```csharp
public interface IOperationEventSink
{
    void OnOperationCompleted(OperationCompletedContext context);
}
```

`OperationCompletedContext` must contain:

```csharp
public sealed class OperationCompletedContext
{
    public string Name { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool IsFailure { get; init; }
    public int ConcurrencyAtEnd { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public double? ProcessCpuPercent { get; init; }
}
```

All sink invocations must be wrapped in try/catch. Exceptions from sinks must not propagate into user code.

### 5.2 Dispatch Mode & Queue

```csharp
public enum EventDispatchMode
{
    Inline,
    BackgroundQueue
}

public enum EventDropPolicy
{
    DropNew
}

public EventDispatchMode EventDispatchMode { get; set; }
public int EventQueueCapacity { get; set; }
public EventDropPolicy EventDropPolicy { get; set; }
public int EventDropLogThrottleSeconds { get; set; }
```

**Defaults:**

- `EventDispatchMode = EventDispatchMode.BackgroundQueue`
- `EventQueueCapacity = 8192`
- `EventDropPolicy = EventDropPolicy.DropNew`
- `EventDropLogThrottleSeconds = 60`

#### Inline Mode

- `OnOperationCompleted` must synchronously call all registered sinks on the calling thread.
- Each sink call must be wrapped in try/catch.

#### Background Queue Mode (default)

- `OnOperationCompleted` must:
  - Build an `OperationCompletedContext`.
  - Attempt to enqueue it into a bounded queue of capacity `EventQueueCapacity`.
- If the queue is full and `EventDropPolicy.DropNew` is selected:
  - Drop the new event without blocking the caller.
  - Increment a dropped events counter.
  - Optionally log a warning, throttled to at most once per `EventDropLogThrottleSeconds`.
- A single (or small fixed number of) background workers must:
  - Dequeue events and call sinks synchronously per event.
  - Wrap each sink call in try/catch.

The library must never block application code due to event sink backpressure while in `BackgroundQueue` mode.

---

## 6. Snapshot Shape, Immutability & Serialization

### 6.1 Types

Snapshots must be simple POCO or record types, including:

- `MonitoringSnapshot`
- `OperationSnapshot`
- `TimeSeriesPoint`

Example:

```csharp
public sealed class MonitoringSnapshot
{
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<OperationSnapshot> Operations { get; init; }
        = Array.Empty<OperationSnapshot>();
}

public sealed class OperationSnapshot
{
    public string Name { get; init; } = string.Empty;
    public long TotalCount { get; init; }
    public long TotalFailures { get; init; }
    public double CurrentRatePerSecond { get; init; }
    public TimeSpan CurrentAverageDuration { get; init; }
    public TimeSpan CurrentMaxDuration { get; init; }
    public IReadOnlyList<TimeSeriesPoint> PerMinute { get; init; }
        = Array.Empty<TimeSeriesPoint>();
    public IReadOnlyList<TimeSeriesPoint> PerHour { get; init; }
        = Array.Empty<TimeSeriesPoint>();
}

public sealed class TimeSeriesPoint
{
    public DateTimeOffset Timestamp { get; init; }
    public long Count { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public TimeSpan MaxDuration { get; init; }
}
```

### 6.2 Immutability & Thread Safety

- All snapshot types must use read-only or init-only properties.
- Once a snapshot is returned from `IMonitoringSnapshotProvider`, it must not be mutated.
- Snapshots must be safe to use concurrently from multiple threads (no mutable internal state).

### 6.3 Serialization Requirements

- No JSON attributes are required; types must be serializable by `System.Text.Json` out of the box.
- Naming conventions: public properties in PascalCase.
- Snapshots must not contain self-references, cycles, or non-serializable types.
- The library itself does not depend on any serializer; serialization is the consumer's responsibility.

---

## 7. CPU Timing - Precision & Overhead

### 7.1 v1 CPU Strategy

- Per-operation CPU timing is out of scope for v1. Any `CpuTime` per operation must be absent or always `null`.
- CPU information in v1 is process-level only:
  - When `EnableCpuMeasurement = true`:
    - A background sampler must:
      - Run every `CpuSampleIntervalSeconds`.
      - Read `Process.GetCurrentProcess().TotalProcessorTime`.
      - Compute CPU percent = (delta CPU time) / (delta wall time x core count) x 100.
      - Store CPU percent samples in a ring buffer of length `CpuSampleHistoryCount`.
- `OperationCompletedContext.ProcessCpuPercent` must expose the latest CPU percent sample from this ring buffer (or `null` if not yet sampled).

### 7.2 Overhead Expectations

- CPU sampling occurs at most once per `CpuSampleIntervalSeconds` (default: 1 second).
- Accepted overhead:
  - On a typical server-class dev machine, CPU sampling must add < 0.1 ms per sample on average (design target validated via benchmarks).
  - For typical workloads, CPU sampling overhead must be negligible compared to application workload (<1% CPU utilization at 100k operations per second).
- If this target is not met in benchmarks, document the cost and rely on the default `EnableCpuMeasurement = false` to avoid penalties.

---

## 8. Testing & Benchmark Criteria

### 8.1 Unit Testing Requirements

- Coverage target: at least 80% line coverage for `KoreForge.Metrics.Core` (excluding trivial properties/constructors).
- Must include tests for:
  1. **Bucket rollover** - hot, warm, cold windows roll over correctly and reset overwritten buckets.
  2. **Aggregation correctness** - averages, min, max, counts are correct given synthetic inputs.
  3. **Sampling behavior** - `SamplingRate = 1` measures every call; `SamplingRate = N` keeps counts correct while timing/events fire every Nth call.
  4. **Overflow policy** - when `MaxOperationCount` is reached, new operation names do not get metrics.
  5. **Tag limits** - extra tags are ignored; keys/values are truncated to configured lengths.
  6. **Event dispatch modes** - inline and background queue behavior, including drop accounting when the queue is full.
  7. **TimeMode behavior** - UTC mode uses UTC timestamps; Local mode aligns with `DateTimeOffset.Now`.
  8. **Concurrency** - multiple threads calling `Begin/Dispose` produce consistent counts without races.

### 8.2 Benchmark Requirements

Use `BenchmarkDotNet` on a reference dev machine (document the hardware in the repo):

1. `Begin/Dispose` with no sinks and no CPU measurement.
   - Target: < 300 ns per call (baseline 1M ops/sec scale).
2. `Begin/Dispose` with background queue dispatch and one no-op sink.
   - Target: < 1 microsecond per call under moderate load.
3. Sampling rate sweep demonstrating overhead reduction at higher `SamplingRate` values.
4. CPU sampling overhead comparing `EnableCpuMeasurement` true vs false at a fixed throughput.

**Acceptance:**

- The library should keep overhead below roughly 5% of a trivial "empty method" at 100k operations/sec in the reference benchmark.
- If not, document performance characteristics and ensure defaults (`SamplingRate = 1`, `EnableCpuMeasurement = false`) remain safe for typical production loads (<100k operations/sec).

---

## 9. ASP.NET Integration (Explicit Opt-in)

### 9.1 Hidden Base Controller

```csharp
public abstract class MonitoringControllerBase : ControllerBase
{
    protected MonitoringControllerBase(IMonitoringSnapshotProvider snapshots) { }

    [NonAction]
    protected ActionResult<MonitoringSnapshot> GetSnapshotCore() { }
}
```

- No route attributes on the class.
- Public or protected methods that return data must be `[NonAction]` or protected.
- The library must not define any concrete controllers; ASP.NET Core exposes nothing by default.

### 9.2 Minimal API Extension

```csharp
public static class MonitoringEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapMonitoringEndpoints(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/monitoring");
}
```

This method must:

- Create a route group with the given `pattern`.
- Map endpoints (for example `GET /snapshot`) inside that group.
- Rely on DI to obtain `IMonitoringSnapshotProvider`.

No endpoints are registered unless the user explicitly calls `MapMonitoringEndpoints` or creates a concrete controller deriving from `MonitoringControllerBase`.

---

This specification removes ambiguity. If needed, we can next generate a C# skeleton (interfaces, options class with defaults, basic extension methods) that matches this spec and is ready to drop into a new solution.

