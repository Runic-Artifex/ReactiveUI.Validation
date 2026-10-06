// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVE_SHIM
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
#else
using ReactiveUI.Primitives.Concurrency;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests.Models;
#else
namespace ReactiveUI.Validation.Tests.Models;
#endif

/// <summary>A deterministic queue for immediate model-compatible scheduled work.</summary>
internal sealed class ValidationManualScheduler :
#if REACTIVE_SHIM
    IScheduler
#else
    ISequencer
#endif
{
    /// <summary>Pending work in serialized insertion order.</summary>
    private readonly Queue<Action> _queue = new();

    /// <summary>Gets the fixed clock used by immediate-only tests.</summary>
    public DateTimeOffset Now => DateTimeOffset.UnixEpoch;

#if REACTIVE_SHIM
    /// <inheritdoc/>
    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
    {
        var lifetime = new SerialDisposable();
        _queue.Enqueue(() =>
        {
            if (!lifetime.IsDisposed)
            {
                lifetime.Disposable = action(this, state);
            }
        });
        return lifetime;
    }

    /// <inheritdoc/>
    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        if (dueTime > TimeSpan.Zero)
        {
            throw new NotSupportedException("These tests schedule immediate work only.");
        }

        return Schedule(state, action);
    }

    /// <inheritdoc/>
    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) =>
        Schedule(state, dueTime - Now, action);
#else
    /// <inheritdoc/>
    public long Timestamp => 0;

    /// <inheritdoc/>
    public void Schedule(IWorkItem item) => _queue.Enqueue(item.Execute);

    /// <inheritdoc/>
    public void Schedule(IWorkItem item, long dueTimestamp)
    {
        if (dueTimestamp > Timestamp)
        {
            throw new NotSupportedException("These tests schedule immediate work only.");
        }

        Schedule(item);
    }
#endif

    /// <summary>Runs queued work, including work queued by earlier notifications, to quiescence.</summary>
    internal void Drain()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
