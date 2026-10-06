// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Tracks explicit validation origins and external replacement epochs on a serialized storage owner.</summary>
/// <remarks>Call <see cref="RecordChange"/> before publishing storage notifications. Origins created by another policy are external changes. This contract does not infer
/// origins from ordinary property notifications.</remarks>
[System.Diagnostics.DebuggerDisplay("Revision = {Revision}, ExternalRevision = {ExternalRevision}")]
public sealed class ValidationStoragePolicy
{
    /// <summary>Nested peer writes awaiting the active copy-back scope.</summary>
    private readonly Queue<(ValidationWriteOrigin Origin, long Sequence)> _writes = new();

    /// <summary>Latest pending work for each independent writer.</summary>
    private readonly Dictionary<ValidationWriteOrigin, (long Sequence, Action Write)> _pending = new();

    /// <summary>The serialized write admission sequence.</summary>
    private long _sequence;

    /// <summary>The failure being delivered to canceled scope writers.</summary>
    private Exception? _failureInProgress;

    /// <summary>Whether a peer write scope is draining.</summary>
    private int _writing;

    /// <summary>Gets the revision of every recorded storage change, including equal-valued writes.</summary>
    public long Revision { get; private set; }

    /// <summary>Gets the revision of external replacements; peer validation writes preserve this epoch.</summary>
    public long ExternalRevision { get; private set; }

    /// <summary>Gets the last explicit origin, or null for ordinary external writes.</summary>
    public object? LastOrigin { get; private set; }

    /// <summary>Creates an independent whole-storage writer receipt.</summary>
    /// <returns>A receipt scoped to this storage policy.</returns>
    public ValidationWriteReceipt CreateReceipt() => new(this, null);

    /// <summary>Creates an independent writer receipt with an explicit structural slot.</summary>
    /// <param name="slot">The writer's declared slot identity.</param>
    /// <returns>A receipt scoped to this storage policy.</returns>
    public ValidationWriteReceipt CreateReceipt(ValidationPath slot)
    {
        ArgumentExceptionHelper.ThrowIfNull(slot);
        return new(this, slot);
    }

    /// <summary>Serializes typed peer construction and copy-back, deferring nested writes until current storage is committed.</summary>
    /// <param name="origin">A writer belonging to this policy.</param>
    /// <param name="write">Acquires current storage and performs complete typed assignment when dequeued.</param>
    /// <exception cref="ArgumentException">The origin belongs to a different storage policy.</exception>
    /// <remarks>Nested writes coalesce by writer and execute in latest admission order. Failure clears pending work and propagates to the draining caller.
    /// Owned ValidationDependency.Writer registrations fail the failed and discarded admitted writers before propagation.
    /// Custom bound plans must attach ValidationDependency.Writer(receipt) for queued failure cleanup. Direct mutation during construction is a conflict.</remarks>
    public void Execute(ValidationWriteOrigin origin, Action write)
    {
        ArgumentExceptionHelper.ThrowIfNull(origin);
        ArgumentExceptionHelper.ThrowIfNull(write);
        if (!ReferenceEquals(origin.Policy, this))
        {
            throw new ArgumentException("The writer belongs to another storage policy.", nameof(origin));
        }

        if (_failureInProgress is { } scopeError)
        {
            ExceptionDispatchInfo.Capture(scopeError).Throw();
        }

        if (origin.IsDisposed)
        {
            return;
        }

        var sequence = ++_sequence;
        origin.IsPending = true;
        origin.PendingExternalRevision = ExternalRevision;
        _pending[origin] = (sequence, write);
        _writes.Enqueue((origin, sequence));
        if (Interlocked.Exchange(ref _writing, 1) != 0)
        {
            return;
        }

        try
        {
            Drain();
        }
        finally
        {
            _failureInProgress = null;
            Volatile.Write(ref _writing, 0);
            _writes.Clear();
            foreach (var writer in _pending.Keys)
            {
                writer.IsPending = false;
            }

            _pending.Clear();
        }
    }

    /// <summary>Serializes typed replacement construction and rejects direct storage mutation before complete copy-back.</summary>
    /// <typeparam name="TStorage">The complete enclosing storage type.</typeparam>
    /// <typeparam name="TValue">The selected output type.</typeparam>
    /// <param name="receipt">The independently owned writer receipt.</param>
    /// <param name="read">Acquires current complete storage when dequeued.</param>
    /// <param name="value">The complete selected output.</param>
    /// <param name="transform">Constructs replacement storage without directly changing the owner.</param>
    /// <param name="commit">Stores replacement and records its origin before notifying observers.</param>
    /// <remarks>Peer operations coalesce by origin and serialize on current storage. Custom bound plans must attach ValidationDependency.Writer(receipt).
    /// Direct mutation during read or transform raises a conflict; reentrant external changes during final commit remain external epochs.</remarks>
    public void Write<TStorage, TValue>(
        ValidationWriteReceipt receipt,
        Func<TStorage> read,
        TValue value,
        Func<TStorage, TValue, TStorage> transform,
        Action<TStorage, ValidationWriteOrigin> commit)
    {
        ArgumentExceptionHelper.ThrowIfNull(receipt);
        ArgumentExceptionHelper.ThrowIfNull(read);
        ArgumentExceptionHelper.ThrowIfNull(transform);
        ArgumentExceptionHelper.ThrowIfNull(commit);
        Execute(receipt.Origin, () =>
        {
            var revision = Revision;
            var storage = read();
            if (receipt.Origin.IsDisposed)
            {
                return;
            }

            var replacement = transform(storage, value);
            if (receipt.Origin.IsDisposed)
            {
                return;
            }

            if (Revision != revision)
            {
                throw new InvalidOperationException("Storage changed externally during replacement construction. Use a pure typed transformation and the explicit peer write scope.");
            }

            receipt.Capture();
            commit(replacement, receipt.Origin);
        });
    }

    /// <summary>Records complete storage replacement before its observers are notified.</summary>
    /// <param name="origin">The receipt origin for validation writes, or null for external writes.</param>
    public void RecordChange(object? origin)
    {
        LastOrigin = origin;
        Revision++;
        if (origin is not ValidationWriteOrigin writer || writer.IsDisposed || !ReferenceEquals(writer.Policy, this))
        {
            ExternalRevision++;
        }
    }

    /// <summary>Releases pending captures belonging to a disposed writer.</summary>
    /// <param name="origin">The independent writer identity.</param>
    internal void Cancel(ValidationWriteOrigin origin)
    {
        origin.IsPending = false;
        _ = _pending.Remove(origin);
    }

    /// <summary>Runs latest admitted writer operations in their serialized order.</summary>
    private void Drain()
    {
        while (_writes.TryDequeue(out var writer))
        {
            if (!_pending.TryGetValue(writer.Origin, out var pending) || pending.Sequence != writer.Sequence)
            {
                continue;
            }

            _ = _pending.Remove(writer.Origin);
            writer.Origin.IsPending = false;
            if (!writer.Origin.IsDisposed)
            {
                try
                {
                    pending.Write();
                }
                catch (Exception error)
                {
                    FailScope(writer.Origin, error);
                    throw;
                }
            }
        }
    }

    /// <summary>Fails every admitted writer whose work cannot finish in the failed scope.</summary>
    /// <param name="failed">The writer whose operation failed.</param>
    /// <param name="error">The original producer failure.</param>
    /// <exception cref="AggregateException">A writer failure relay also failed during cleanup.</exception>
    private void FailScope(ValidationWriteOrigin failed, Exception error)
    {
        List<ValidationWriteOrigin> origins = [failed];
        foreach (var origin in _pending.Keys)
        {
            if (!ReferenceEquals(origin, failed))
            {
                origins.Add(origin);
            }
        }

        _failureInProgress = error;
        _pending.Clear();
        _writes.Clear();
        List<Exception>? failures = null;
        foreach (var origin in origins)
        {
            origin.IsPending = false;
            try
            {
                origin.Failure?.Invoke(error);
            }
            catch (Exception relay)
            {
                if (!ReferenceEquals(error, relay))
                {
                    (failures ??= [error]).Add(relay);
                }
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}
