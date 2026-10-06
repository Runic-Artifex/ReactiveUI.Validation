// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
#if REACTIVE_SHIM
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
#else
using ReactiveUI.Primitives.Concurrency;
#endif

internal sealed class CapabilityScheduler :
#if REACTIVE_SHIM
    IScheduler
#else
    ISequencer
#endif
{
    private readonly Queue<Action> _queue = [];
    public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
#if REACTIVE_SHIM
    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
    {
        var lifetime = new SerialDisposable();
        _queue.Enqueue(() =>
        {
            if (!lifetime.IsDisposed) lifetime.Disposable = action(this, state);
        });
        return lifetime;
    }
    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        if (dueTime > TimeSpan.Zero) throw new NotSupportedException("The explicit acceptance scheduler supports immediate work only.");
        return Schedule(state, action);
    }
    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) => Schedule(state, dueTime - Now, action);
#else
    public long Timestamp => 0;
    public void Schedule(IWorkItem item) => _queue.Enqueue(item.Execute);
    public void Schedule(IWorkItem item, long dueTimestamp)
    {
        if (dueTimestamp > Timestamp) throw new NotSupportedException("The explicit acceptance sequencer supports immediate work only.");
        Schedule(item);
    }
#endif
    internal void Drain()
    {
        while (_queue.TryDequeue(out var action)) action();
    }
}
