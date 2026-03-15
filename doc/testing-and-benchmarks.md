# Testing & Benchmarks

This guide covers how to validate changes before opening a PR.

## Unit Tests

- Run the full suite:

	```powershell
	dotnet test
	```

- The project currently ships with 17 focused xUnit tests covering:
	- Metrics windows (rollover, aggregation, overflow, sampling, concurrency, tags).
	- Event dispatch paths (inline vs. background queue, drop accounting).
	- CPU measurement toggle behavior.
	- Time mode handling via custom clocks.
	- Dependency injection wiring and snapshot composition (see `DependencyInjectionTests` and `SnapshotProviderTests`).
- Use the helper types in `tests/.../Infrastructure` (`TestClock`, `FakeDataSource`, `TestEventSink`) to add deterministic scenarios.

## Coverage

We rely on Coverlet to capture line coverage. Example command:

```powershell
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Aim for 80%+ in `KoreForge.Metrics.Core`, especially for non-trivial helpers such as `MonitoringSnapshotProvider`, `OperationTags`, `MonitoringServiceCollectionExtensions`, and `EventDispatcher`.

## Benchmarks

The `tst/KF.Metrics.Benchmarks` project uses BenchmarkDotNet to track instrumentation overhead. Typical runs:

```powershell
dotnet run -c Release --project tst/KF.Metrics.Benchmarks
```

Benchmarks currently include:

1. **Baseline scopes** - inline dispatch, CPU sampling off.
2. **Background queue scopes** - validates enqueue cost with a no-op sink.
3. **Sampled scopes** - demonstrates the effect of higher `SamplingRate` values.
4. **CPU sampler impact** - compares `EnableCpuMeasurement` true vs false.

Update benchmark descriptions whenever you add a new scenario so downstream consumers know what the numbers represent.

## Troubleshooting Tips

- If a test stalls, ensure all `MonitoringEngine` instances are disposed (use `using` blocks in tests).
- Background-queue tests rely on `ManualResetEventSlim`. Keep timeouts generous to avoid flakiness on CI.
- Use `TestClock.Advance` whenever you need deterministic timestamp progression.
