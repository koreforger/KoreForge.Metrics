namespace KF.Metrics;

public interface IMonitoringSnapshotProvider
{
    MonitoringSnapshot GetSnapshot();

    OperationSnapshot? GetOperationSnapshot(string name);
}
