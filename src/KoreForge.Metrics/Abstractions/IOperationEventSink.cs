namespace KoreForge.Metrics;

public interface IOperationEventSink
{
    void OnOperationCompleted(OperationCompletedContext context);
}
