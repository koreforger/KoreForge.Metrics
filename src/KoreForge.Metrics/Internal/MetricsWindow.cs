using System;

namespace KoreForge.Metrics;

internal sealed class MetricsWindow
{
    private readonly MetricsBucket[] _buckets;
    private readonly object _gate = new();
    private int _index;

    public MetricsWindow(int bucketCount)
    {
        _buckets = new MetricsBucket[bucketCount];
        for (var i = 0; i < _buckets.Length; i++)
        {
            _buckets[i].Reset();
        }
    }

    public int BucketCount => _buckets.Length;

    public int Index => _index;

    public void AddSample(long durationTicks)
    {
        lock (_gate)
        {
            if (_buckets.Length == 0)
            {
                return;
            }

            _buckets[_index].AddSample(durationTicks);
        }
    }

    public void Advance()
    {
        lock (_gate)
        {
            if (_buckets.Length == 0)
            {
                return;
            }

            _index = (_index + 1) % _buckets.Length;
            _buckets[_index].Reset();
        }
    }

    public MetricsBucket[] SnapshotBuckets()
    {
        lock (_gate)
        {
            var clone = new MetricsBucket[_buckets.Length];
            Array.Copy(_buckets, clone, _buckets.Length);
            return clone;
        }
    }
}
