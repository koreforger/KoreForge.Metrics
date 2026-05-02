using System;
using System.Threading;

namespace KoreForge.Metrics;

internal sealed class OperationMetrics
{
    private readonly MetricsWindow _hotWindow;
    private readonly MetricsWindow _warmWindow;
    private readonly MetricsWindow _coldWindow;
    private long _totalCount;
    private long _totalFailures;
    private int _currentInFlight;
    private int _peakInFlight;
    private int _samplingCounter;

    public OperationMetrics(string name, MonitoringOptions options)
    {
        Name = name;
        _hotWindow = new MetricsWindow(Math.Max(1, options.HotBucketCount));
        _warmWindow = new MetricsWindow(Math.Max(1, options.WarmBucketCount));
        _coldWindow = new MetricsWindow(Math.Max(1, options.ColdBucketCount));
    }

    public string Name { get; }

    public int RecordStart()
    {
        Interlocked.Increment(ref _totalCount);
        var inFlight = Interlocked.Increment(ref _currentInFlight);
        var snapshot = inFlight;
        while (true)
        {
            var currentPeak = Volatile.Read(ref _peakInFlight);
            if (snapshot <= currentPeak)
            {
                break;
            }

            if (Interlocked.CompareExchange(ref _peakInFlight, snapshot, currentPeak) == currentPeak)
            {
                break;
            }
        }

        return inFlight;
    }

    public int RecordCompletion(long durationTicks, bool isFailure, bool recordTiming)
    {
        if (isFailure)
        {
            Interlocked.Increment(ref _totalFailures);
        }

        var remaining = Interlocked.Decrement(ref _currentInFlight);

        if (!recordTiming || durationTicks <= 0)
        {
            return remaining;
        }

        _hotWindow.AddSample(durationTicks);
        _warmWindow.AddSample(durationTicks);
        _coldWindow.AddSample(durationTicks);
        return remaining;
    }

    public bool ShouldCaptureSample(int samplingRate)
    {
        if (samplingRate <= 1)
        {
            return true;
        }

        var count = Interlocked.Increment(ref _samplingCounter);
        return count % samplingRate == 0;
    }

    public void AdvanceHot() => _hotWindow.Advance();

    public void AdvanceWarm() => _warmWindow.Advance();

    public void AdvanceCold() => _coldWindow.Advance();

    public OperationMetricsSnapshotData CaptureSnapshot()
    {
        var totalCount = Volatile.Read(ref _totalCount);
        var totalFailures = Volatile.Read(ref _totalFailures);
        var currentInFlight = Volatile.Read(ref _currentInFlight);
        var peak = Volatile.Read(ref _peakInFlight);

        return new OperationMetricsSnapshotData(
            Name,
            totalCount,
            totalFailures,
            currentInFlight,
            peak,
            _hotWindow.SnapshotBuckets(),
            _hotWindow.Index,
            _warmWindow.SnapshotBuckets(),
            _warmWindow.Index,
            _coldWindow.SnapshotBuckets(),
            _coldWindow.Index);
    }
}
