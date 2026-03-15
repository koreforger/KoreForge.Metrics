namespace KF.Metrics;

internal struct MetricsBucket
{
    public long Count;
    public long TotalTicks;
    public long MaxTicks;
    public long MinTicks;

    public void Reset()
    {
        Count = 0;
        TotalTicks = 0;
        MaxTicks = 0;
        MinTicks = long.MaxValue;
    }

    public void AddSample(long durationTicks)
    {
        Count++;
        TotalTicks += durationTicks;
        if (durationTicks > MaxTicks)
        {
            MaxTicks = durationTicks;
        }

        if (durationTicks < MinTicks)
        {
            MinTicks = durationTicks;
        }
    }
}
