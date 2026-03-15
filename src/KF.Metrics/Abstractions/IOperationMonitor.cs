namespace KF.Metrics;

public interface IOperationMonitor
{
    OperationScope Begin(string name, OperationTags? tags = null);
}
