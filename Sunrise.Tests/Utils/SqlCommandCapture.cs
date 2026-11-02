using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sunrise.Tests.Utils;

public sealed class SqlCommandCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly ConcurrentBag<IDisposable> _subscriptions = new();
    public ConcurrentQueue<string> Commands { get; } = new();

    public SqlCommandCapture()
    {
        _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "Microsoft.EntityFrameworkCore")
            _subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuting.Name));
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Value is CommandEventData data)
            Commands.Enqueue(data.Command.CommandText);
    }

    public void OnCompleted() { }
    public void OnError(Exception error) { }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
    }
}
