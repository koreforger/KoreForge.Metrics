using System.Collections.Generic;

namespace KoreForge.Metrics;

internal interface IMonitoringDataSource
{
    MonitoringOptions CurrentOptions { get; }

    IEnumerable<OperationMetricsSnapshotData> CaptureSnapshots();
}
