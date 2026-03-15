using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KF.Metrics;

internal sealed class ProcessCpuSampler : IDisposable
{
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly double[] _samples;
    private readonly CancellationTokenSource? _cts;
    private readonly Task? _worker;
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private int _index;
    private int _count;
    private bool _disposed;

    public ProcessCpuSampler(MonitoringOptions options, TimeProvider timeProvider)
    {
        _enabled = options.EnableCpuMeasurement;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.CpuSampleIntervalSeconds));
        _samples = new double[Math.Max(1, options.CpuSampleHistoryCount)];
        _timeProvider = timeProvider;

        if (_enabled)
        {
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public double? LatestSample
    {
        get
        {
            if (!_enabled)
            {
                return null;
            }

            lock (_gate)
            {
                if (_count == 0)
                {
                    return null;
                }

                return _samples[_index];
            }
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        var lastWall = _timeProvider.GetUtcNow();
        var lastCpu = TimeSpan.Zero;
        var hasBaseline = false;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var process = Process.GetCurrentProcess();
                var currentCpu = process.TotalProcessorTime;
                var currentWall = _timeProvider.GetUtcNow();

                if (!hasBaseline)
                {
                    hasBaseline = true;
                    lastCpu = currentCpu;
                    lastWall = currentWall;
                    continue;
                }

                var cpuDelta = currentCpu - lastCpu;
                var wallDelta = currentWall - lastWall;

                lastCpu = currentCpu;
                lastWall = currentWall;

                if (wallDelta <= TimeSpan.Zero)
                {
                    continue;
                }

                var coreCount = Environment.ProcessorCount;
                if (coreCount <= 0)
                {
                    continue;
                }

                var percent = cpuDelta.TotalMilliseconds / (wallDelta.TotalMilliseconds * coreCount) * 100d;
                if (double.IsNaN(percent) || double.IsInfinity(percent))
                {
                    continue;
                }

                if (percent < 0)
                {
                    percent = 0;
                }

                StoreSample(Math.Min(100d, percent));
            }
            catch (Exception)
            {
                // Fail-open: ignore sampling errors.
            }
        }
    }

    private void StoreSample(double value)
    {
        lock (_gate)
        {
            _index = (_index + 1) % _samples.Length;
            _samples[_index] = value;
            if (_count < _samples.Length)
            {
                _count++;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception)
        {
            // Ignore.
        }

        _cts?.Dispose();
    }
}
