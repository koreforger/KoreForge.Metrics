using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace KoreForge.Metrics;

internal sealed class EventDispatcher : IDisposable
{
    private readonly IOperationEventSink[] _sinks;
    private readonly object _gate = new();
    private Channel<OperationCompletedContext>? _channel;
    private CancellationTokenSource? _cts;
    private Task? _consumer;
    private EventDispatchMode _mode;
    private long _droppedEvents;
    private bool _disposed;

    public EventDispatcher(IEnumerable<IOperationEventSink> sinks)
    {
        _sinks = sinks.ToArray();
        _mode = EventDispatchMode.BackgroundQueue;
    }

    public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

    public void UpdateOptions(MonitoringOptions options)
    {
        lock (_gate)
        {
            if (_mode == options.EventDispatchMode && options.EventDispatchMode == EventDispatchMode.Inline)
            {
                _mode = options.EventDispatchMode;
                return;
            }

            _mode = options.EventDispatchMode;
            if (_mode == EventDispatchMode.Inline)
            {
                StopBackgroundQueue();
                return;
            }

            StartBackgroundQueue(options.EventQueueCapacity);
        }
    }

    public void Publish(OperationCompletedContext context)
    {
        if (_sinks.Length == 0)
        {
            return;
        }

        if (_mode == EventDispatchMode.Inline)
        {
            DispatchInline(context);
            return;
        }

        var channel = _channel;
        if (channel is null)
        {
            DispatchInline(context);
            return;
        }

        if (!channel.Writer.TryWrite(context))
        {
            Interlocked.Increment(ref _droppedEvents);
        }
    }

    private void DispatchInline(OperationCompletedContext context)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                sink.OnOperationCompleted(context);
            }
            catch
            {
                // Swallow sink exceptions.
            }
        }
    }

    private void StartBackgroundQueue(int capacity)
    {
        StopBackgroundQueue();
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<OperationCompletedContext>(capacity);
        _consumer = Task.Run(() => ConsumeAsync(_cts.Token));
    }

    private async Task ConsumeAsync(CancellationToken token)
    {
        if (_channel is null)
        {
            return;
        }

        var reader = _channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    DispatchInline(item);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dispose path.
        }
    }

    private void StopBackgroundQueue()
    {
        _cts?.Cancel();
        try
        {
            _consumer?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore shutdown issues.
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _consumer = null;
            _channel = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopBackgroundQueue();
    }
}
