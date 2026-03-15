# Architecture Overview

This document explains the moving pieces inside **KoreForge.Metrics** so contributors know where logic lives and how data flows through the system.

## Responsibilities at a Glance

- **OperationMonitor** - public entry point that instruments code paths and constructs `OperationScope` objects.
- **MonitoringEngine** - owns operation registries, background rotation timers, CPU sampling, and the event dispatcher.
- **Metrics windows** - hot/warm/cold circular buffers that aggregate counts, averages, and max values without unbounded memory growth.
- **EventDispatcher** - pushes `OperationCompletedContext` instances to sinks either inline or through a bounded background queue with drop tracking.
- **MonitoringSnapshotProvider** - composes immutable `MonitoringSnapshot` and `OperationSnapshot` instances for diagnostics endpoints.
- **ProcessCpuSampler** - optional background sampler that writes the latest process-level CPU percentage for inclusion in events.

## Component Interactions

1. **Instrumentation** - application code resolves `IOperationMonitor` and calls `Begin(name, tags)`. The monitor sanitizes tags, applies sampling, and records the start timestamp from the configured `ISystemClock`.
2. **Metrics capture** - when a scope is disposed, `OperationMetrics` updates counters, aggregates duration buckets, and returns the concurrency-at-end value.
3. **Event routing** - if sampling keeps the call, an `OperationCompletedContext` is built and sent to `MonitoringEngine.PublishEvent`, which defers to `EventDispatcher`.
4. **Background maintenance** - the engine rotates hot/warm/cold windows on its own timer thread and advances each registered `OperationMetrics` instance in lock-step.
5. **Snapshot generation** - `MonitoringSnapshotProvider` asks the engine for `OperationMetricsSnapshotData`, stitches per-operation stats, and stamps results with the `ISystemClock` according to the global time mode.

## Event Dispatch Policies

- `EventDispatchMode.Inline` invokes every sink on the caller's thread. Failures are swallowed to keep instrumentation safe.
- `EventDispatchMode.BackgroundQueue` uses a bounded channel. When the queue fills, the dispatcher drops the new event (matching `EventDropPolicy.DropNew`), increments the public `DroppedEvents` counter, and throttles log notifications. This ensures producers are never blocked by sinks.

## Threading & Safety Considerations

- `OperationMetrics` relies on `Interlocked` operations so increments can happen concurrently without locks.
- Ring buffers are allocated once and reused; advancing a window resets the next slot instead of allocating a new container.
- `MonitoringEngine` serializes operation creation behind a single lock to enforce the `MaxOperationCount` limit while keeping steady-state reads lock-free.
- `MonitoringSnapshotProvider` only constructs immutable POCOs, so consumers can cache or serialize results without additional synchronization.

## Extension Points

- **Event sinks** - implement `IOperationEventSink` and register it in DI to observe slow or hot operations.
- **ASP.NET opt-in** - `MonitoringControllerBase` and `MapMonitoringEndpoints` expose snapshots only when explicitly wired up.
- **Future drop policies** - the dispatcher centralizes drop decisions, making it straightforward to add modes like "drop oldest" without touching call sites.
