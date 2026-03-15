using System.Collections.Generic;

namespace KF.Metrics;

internal interface IMonitoringDataSource
{
    MonitoringOptions CurrentOptions { get; }

    IEnumerable<OperationMetricsSnapshotData> CaptureSnapshots();
}
