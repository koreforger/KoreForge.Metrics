# Developer Integration Guide

Follow these short recipes to wire KoreForge.Metrics into an application.

## 1. Register Services

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKoreForgeMetrics(options =>
{
	options.TimeMode = MonitoringTimeMode.Local;
	options.SamplingRate = 1;
	options.EnableCpuMeasurement = false;
});
```

The extension adds:
- `MonitoringEngine` and `IOperationMonitor` singletons.
- `ISystemClock` (UTC or Local based on `MonitoringOptions`).
- `IMonitoringSnapshotProvider` for diagnostics endpoints.
- `IMonitoringDataSource` so advanced callers can poll metrics directly.

## 2. Instrument Code

```csharp
public class CheckoutService
{
	private readonly IOperationMonitor _monitor;

	public CheckoutService(IOperationMonitor monitor)
	{
		_monitor = monitor;
	}

	public async Task PlaceOrderAsync(Order order)
	{
		using var scope = _monitor.Begin("checkout", new OperationTags
		{
			{"region", order.Region},
			{"channel", order.Channel}
		});

		try
		{
			await _processor.RunAsync(order);
		}
		catch
		{
			scope.MarkFailed();
			throw;
		}
	}
}
```

Tags are sanitized according to `MonitoringOptions` limits, and disposing the scope is enough to push metrics and events.

## 3. Provide Event Sinks

Implement `IOperationEventSink` to react to slow operations:

```csharp
public sealed class AlertingSink : IOperationEventSink
{
	public void OnOperationCompleted(OperationCompletedContext context)
	{
		if (context.Duration > TimeSpan.FromSeconds(1))
		{
			_logger.LogWarning(
				"Slow {Operation} ({Duration} ms)",
				context.Name,
				context.Duration.TotalMilliseconds);
		}
	}
}
```

Register sinks like any DI service; the engine injects `IEnumerable<IOperationEventSink>` automatically.

## 4. ASP.NET Exposure (Opt-in)

**Controller approach**

```csharp
[Route("api/monitoring")]
public sealed class MonitoringController : MonitoringControllerBase
{
	public MonitoringController(IMonitoringSnapshotProvider snapshots)
		: base(snapshots) { }

	[HttpGet("snapshot")]
	public ActionResult<MonitoringSnapshot> Get() => GetSnapshotCore();
}
```

**Minimal API approach**

```csharp
var app = builder.Build();
app.MapMonitoringEndpoints("/monitoring");
```

No endpoints are published unless you opt in via one of these paths.

## 5. Background Queue Guidance

- Default mode is `EventDispatchMode.BackgroundQueue` with `EventQueueCapacity = 8192`.
- Use `EventDropPolicy.DropNew` (current implementation) to keep callers non-blocking.
- Watch `MonitoringEngine.DroppedEvents` or expose it via metrics to know when sinks are overwhelmed.

## 6. Benchmarking Hook

Run `dotnet run -c Release --project tst/KoreForge.Metrics.Benchmarks` to evaluate instrumentation overhead whenever you change critical code paths.

