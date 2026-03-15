namespace KF.Metrics;

public interface IOperationEventSink
{
    void OnOperationCompleted(OperationCompletedContext context);
}
