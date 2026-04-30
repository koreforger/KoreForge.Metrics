using System.Collections.ObjectModel;

namespace KF.Metrics.Flow;

public interface IPipelineFlowMonitor
{
    void RegisterGraph(PipelineGraphDefinition graph);

    void IncrementNodeIn(string nodeId, long count = 1, MetricTags? tags = null);

    void IncrementNodeOut(string nodeId, long count = 1, MetricTags? tags = null);

    void IncrementNodeFailure(string nodeId, long count = 1, MetricTags? tags = null);

    IDisposable TrackNodeLatency(string nodeId, MetricTags? tags = null);

    void SetNodeBacklog(string nodeId, long value, MetricTags? tags = null);

    void IncrementEdgeTransfer(string fromNodeId, string toNodeId, long count = 1, MetricTags? tags = null);

    PipelineGraphSnapshot GetSnapshot(string graphId);

    IReadOnlyList<PipelineGraphDefinition> GetGraphs();
}

public sealed record MetricTags(IReadOnlyDictionary<string, string> Tags)
{
    public static MetricTags Empty { get; } = new(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
}

public sealed record PipelineGraphDefinition(
    string GraphId,
    string Name,
    IReadOnlyList<PipelineNodeDefinition> Nodes,
    IReadOnlyList<PipelineEdgeDefinition> Edges);

public sealed record PipelineNodeDefinition(
    string NodeId,
    string Name,
    string Kind,
    string? ParentNodeId,
    int DisplayOrder);

public sealed record PipelineEdgeDefinition(
    string FromNodeId,
    string ToNodeId,
    string Name);

public sealed record PipelineGraphSnapshot(
    string GraphId,
    string Name,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PipelineNodeSnapshot> Nodes,
    IReadOnlyList<PipelineEdgeSnapshot> Edges);

public sealed record PipelineNodeSnapshot(
    string NodeId,
    string Name,
    string Kind,
    string? ParentNodeId,
    long InCount,
    long OutCount,
    long FailedCount,
    long CurrentInFlight,
    long BacklogCount,
    double CurrentRatePerSecond,
    double AverageLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double MaxLatencyMs);

public sealed record PipelineEdgeSnapshot(
    string FromNodeId,
    string ToNodeId,
    string Name,
    long TotalTransferred,
    double CurrentRatePerSecond);
