using System;
using System.Diagnostics;

namespace KoreForge.Metrics;

public static class StopwatchTime
{
    private static readonly double TickToSeconds = 1d / Stopwatch.Frequency;

    public static long FromTimeSpan(TimeSpan duration)
        => (long)(duration.TotalSeconds * Stopwatch.Frequency);

    public static TimeSpan ToTimeSpan(double ticks)
    {
        if (ticks <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(ticks * TickToSeconds);
    }
}
