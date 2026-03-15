using System;
using System.Diagnostics;

namespace KF.Metrics.Tests.Infrastructure;

internal sealed class TestClock : TimeProvider
{
	private DateTimeOffset _utcNow;
	private long _timestamp;

	public TestClock(DateTimeOffset? start = null)
	{
		_utcNow = (start ?? DateTimeOffset.UtcNow).ToUniversalTime();
	}

	public override DateTimeOffset GetUtcNow() => _utcNow;

	public override long GetTimestamp() => _timestamp;

	public void Advance(TimeSpan amount)
	{
		_utcNow += amount;
		_timestamp += ToStopwatchTicks(amount);
	}

	public static long ToStopwatchTicks(TimeSpan duration)
		=> (long)(duration.TotalSeconds * Stopwatch.Frequency);
}
