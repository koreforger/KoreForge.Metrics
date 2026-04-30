using System.Collections.Concurrent;
using System.Diagnostics;

namespace KF.Metrics.Flow;

public sealed class PipelineFlowMonitor : IPipelineFlowMonitor
{
    private readonly ConcurrentDictionary<string, PipelineGraphDefinition> _graphs = new();
    private readonly ConcurrentDictionary<string, NodeMetrics> _nodeMetrics = new();
    private readonly ConcurrentDictionary<string, EdgeMetrics> _edgeMetrics = new();
    private readonly ConcurrentDictionary<string, long> _nodeBacklogs = new();
    private readonly TimeProvider _timeProvider;

    public PipelineFlowMonitor(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void RegisterGraph(PipelineGraphDefinition graph)
    {
        _graphs[graph.GraphId] = graph;

        foreach (var node in graph.Nodes)
        {
            _nodeMetrics.GetOrAdd(node.NodeId, _ => new NodeMetrics());
        }

        foreach (var edge in graph.Edges)
        {
            var key = EdgeKey(edge.FromNodeId, edge.ToNodeId);
            _edgeMetrics.GetOrAdd(key, _ => new EdgeMetrics());
        }
    }

    public void IncrementNodeIn(string nodeId, long count = 1, MetricTags? tags = null)
    {
        var metrics = _nodeMetrics.GetOrAdd(nodeId, _ => new NodeMetrics());
        Interlocked.Add(ref metrics.InCount, count);
        Interlocked.Increment(ref metrics.CurrentInFlight);
    }

    public void IncrementNodeOut(string nodeId, long count = 1, MetricTags? tags = null)
    {
        var metrics = _nodeMetrics.GetOrAdd(nodeId, _ => new NodeMetrics());
        Interlocked.Add(ref metrics.OutCount, count);
        Interlocked.Decrement(ref metrics.CurrentInFlight);
        metrics.RecordCompletion();
    }

    public void IncrementNodeFailure(string nodeId, long count = 1, MetricTags? tags = null)
    {
        var metrics = _nodeMetrics.GetOrAdd(nodeId, _ => new NodeMetrics());
        Interlocked.Add(ref metrics.FailedCount, count);
        Interlocked.Decrement(ref metrics.CurrentInFlight);
    }

    public IDisposable TrackNodeLatency(string nodeId, MetricTags? tags = null)
    {
        var metrics = _nodeMetrics.GetOrAdd(nodeId, _ => new NodeMetrics());
        var start = Stopwatch.GetTimestamp();
        return new LatencyTracker(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            metrics.RecordLatency(elapsed.TotalMilliseconds);
        });
    }

    public void SetNodeBacklog(string nodeId, long value, MetricTags? tags = null)
    {
        _nodeBacklogs[nodeId] = value;
    }

    public void IncrementEdgeTransfer(string fromNodeId, string toNodeId, long count = 1, MetricTags? tags = null)
    {
        var key = EdgeKey(fromNodeId, toNodeId);
        var metrics = _edgeMetrics.GetOrAdd(key, _ => new EdgeMetrics());
        Interlocked.Add(ref metrics.TotalTransferred, count);
        metrics.RecordTransfer();
    }

    public PipelineGraphSnapshot GetSnapshot(string graphId)
    {
        if (!_graphs.TryGetValue(graphId, out var graph))
        {
            throw new ArgumentException($"Graph '{graphId}' not registered.", nameof(graphId));
        }

        var now = _timeProvider.GetUtcNow();

        var nodeSnapshots = new List<PipelineNodeSnapshot>();
        foreach (var node in graph.Nodes)
        {
            var metrics = _nodeMetrics.GetOrAdd(node.NodeId, _ => new NodeMetrics());
            var backlog = _nodeBacklogs.GetValueOrDefault(node.NodeId, 0);

            var inCount = Interlocked.Read(ref metrics.InCount);
            var outCount = Interlocked.Read(ref metrics.OutCount);
            var failedCount = Interlocked.Read(ref metrics.FailedCount);
            var inFlight = Interlocked.Read(ref metrics.CurrentInFlight);

            var ratePerSec = metrics.CalculateRate(now);

            double avgMs = 0, p50 = 0, p95 = 0, p99 = 0, max = 0;
            var samples = metrics.GetLatencySamples();
            if (samples.Count > 0)
            {
                var sorted = samples.OrderBy(x => x).ToList();
                avgMs = sorted.Average();
                p50 = Percentile(sorted, 0.50);
                p95 = Percentile(sorted, 0.95);
                p99 = Percentile(sorted, 0.99);
                max = sorted[^1];
            }

            nodeSnapshots.Add(new PipelineNodeSnapshot(
                node.NodeId,
                node.Name,
                node.Kind,
                node.ParentNodeId,
                inCount,
                outCount,
                failedCount,
                inFlight,
                backlog,
                ratePerSec,
                avgMs,
                p50,
                p95,
                p99,
                max));
        }

        var edgeSnapshots = new List<PipelineEdgeSnapshot>();
        foreach (var edge in graph.Edges)
        {
            var key = EdgeKey(edge.FromNodeId, edge.ToNodeId);
            var metrics = _edgeMetrics.GetOrAdd(key, _ => new EdgeMetrics());
            var transferred = Interlocked.Read(ref metrics.TotalTransferred);
            var rate = metrics.CalculateRate(now);

            edgeSnapshots.Add(new PipelineEdgeSnapshot(
                edge.FromNodeId,
                edge.ToNodeId,
                edge.Name,
                transferred,
                rate));
        }

        return new PipelineGraphSnapshot(
            graph.GraphId,
            graph.Name,
            now,
            nodeSnapshots,
            edgeSnapshots);
    }

    public IReadOnlyList<PipelineGraphDefinition> GetGraphs() => _graphs.Values.ToList();

    private static string EdgeKey(string fromNodeId, string toNodeId) =>
        $"{fromNodeId}->{toNodeId}";

    private static double Percentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (percentile * (sorted.Count - 1));
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        var fraction = index - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }

    private sealed class NodeMetrics
    {
        public long InCount;
        public long OutCount;
        public long FailedCount;
        public long CurrentInFlight;
        private long _completionCount;
        private double _totalLatencyMs;
        private readonly ConcurrentQueue<double> _latencySamples = new();
        private const int MaxSamples = 1000;
        private long _lastTransferCount;
        private long _lastTransferTicks;
        private double _lastRate;

        public void RecordCompletion()
        {
            Interlocked.Increment(ref _completionCount);
        }

        public void RecordLatency(double latencyMs)
        {
            Interlocked.Add(ref _completionCount, 0);
            var total = Interlocked.Exchange(ref _totalLatencyMs, 0);
            _totalLatencyMs = total;
            _latencySamples.Enqueue(latencyMs);
            while (_latencySamples.Count > MaxSamples)
            {
                _latencySamples.TryDequeue(out _);
            }
        }

        public List<double> GetLatencySamples() => _latencySamples.ToList();

        public double CalculateRate(DateTimeOffset now)
        {
            var currentCount = Interlocked.Read(ref OutCount);
            var nowTicks = now.Ticks;
            var lastCount = Interlocked.Read(ref _lastTransferCount);
            var lastTicks = Interlocked.Read(ref _lastTransferTicks);

            if (lastTicks == 0)
            {
                Interlocked.Exchange(ref _lastTransferCount, currentCount);
                Interlocked.Exchange(ref _lastTransferTicks, nowTicks);
                return 0;
            }

            var deltaCount = currentCount - lastCount;
            var deltaSeconds = (nowTicks - lastTicks) / (double)TimeSpan.TicksPerSecond;

            if (deltaSeconds < 0.01)
            {
                return Interlocked.Exchange(ref _lastRate, 0);
            }

            var rate = deltaCount / deltaSeconds;

            Interlocked.Exchange(ref _lastTransferCount, currentCount);
            Interlocked.Exchange(ref _lastTransferTicks, nowTicks);
            Interlocked.Exchange(ref _lastRate, rate);

            return rate;
        }
    }

    private sealed class EdgeMetrics
    {
        public long TotalTransferred;
        private long _lastTransferCount;
        private long _lastTransferTicks;

        public void RecordTransfer()
        {
        }

        public double CalculateRate(DateTimeOffset now)
        {
            var currentCount = Interlocked.Read(ref TotalTransferred);
            var nowTicks = now.Ticks;
            var lastCount = Interlocked.Read(ref _lastTransferCount);
            var lastTicks = Interlocked.Read(ref _lastTransferTicks);

            if (lastTicks == 0)
            {
                Interlocked.Exchange(ref _lastTransferCount, currentCount);
                Interlocked.Exchange(ref _lastTransferTicks, nowTicks);
                return 0;
            }

            var deltaCount = currentCount - lastCount;
            var deltaSeconds = (nowTicks - lastTicks) / (double)TimeSpan.TicksPerSecond;

            if (deltaSeconds < 0.01) return 0;

            Interlocked.Exchange(ref _lastTransferCount, currentCount);
            Interlocked.Exchange(ref _lastTransferTicks, nowTicks);

            return deltaCount / deltaSeconds;
        }
    }

    private sealed class LatencyTracker : IDisposable
    {
        private readonly Action _onDispose;

        public LatencyTracker(Action onDispose) => _onDispose = onDispose;

        public void Dispose() => _onDispose();
    }
}
