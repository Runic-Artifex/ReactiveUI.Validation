// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Handles each external storage epoch once while allowing peer validation writers to share storage.</summary>
/// <remarks>Capture immediately before assignment. Read current complete storage immediately before typed copy-back. Peer writes preserve the epoch; same-slot writes
/// therefore follow last-writer order, while different-slot writes preserve current peer fields.</remarks>
[System.Diagnostics.DebuggerDisplay("IsCurrent = {IsCurrent()}")]
public sealed class ValidationWriteReceipt : IDisposable
{
    /// <summary>The explicit storage policy.</summary>
    private readonly ValidationStoragePolicy _policy;

    /// <summary>The idempotent receipt lifetime latch.</summary>
    private int _disposed;

    /// <summary>The external epoch handled by the most recent assignment.</summary>
    private long _handledExternalRevision;

    /// <summary>The independently owned producer failure observer.</summary>
    private IObserver<ValidationInvalidation>? _failureObserver;

    /// <summary>Whether any assignment has captured an epoch.</summary>
    private bool _hasWrite;

    /// <summary>Initializes a new instance of the <see cref="ValidationWriteReceipt"/> class.</summary>
    /// <param name="policy">The owning policy.</param>
    /// <param name="slot">The declared structural slot, or null for whole-storage ownership.</param>
    internal ValidationWriteReceipt(ValidationStoragePolicy policy, ValidationPath? slot)
    {
        _policy = policy;
        Origin = new(policy, slot) { Failure = Fail };
    }

    /// <summary>Gets the origin to pass to the explicit storage setter.</summary>
    public ValidationWriteOrigin Origin { get; }

    /// <summary>Captures the current external epoch before an assignment can notify reentrantly.</summary>
    public void Capture()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _handledExternalRevision = _policy.ExternalRevision;
        _hasWrite = true;
    }

    /// <summary>Checks whether this writer already handled the current external epoch.</summary>
    /// <returns>True after a captured write until the next external replacement.</returns>
    public bool IsCurrent() => Volatile.Read(ref _disposed) == 0
        && ((_hasWrite && _handledExternalRevision == _policy.ExternalRevision)
            || (Origin.IsPending && Origin.PendingExternalRevision == _policy.ExternalRevision));

    /// <summary>Releases queued work for this writer; repeated disposal has no effect.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Origin.IsDisposed = true;
        Origin.Failure = null;
        _failureObserver = null;
        _policy.Cancel(Origin);
    }

    /// <summary>Captures the current epoch and the resolved dynamic slot before assignment.</summary>
    /// <param name="slot">The current slot, or null for whole-storage ownership.</param>
    internal void CaptureSlot(ValidationPath? slot)
    {
        Origin.SlotIdentity = slot;
        Capture();
    }

    /// <summary>Rejects a stale replacement so the pending storage refresh retries current output.</summary>
    internal void Invalidate() => _hasWrite = false;

    /// <summary>Admits the failure relay for this independently owned writer subscription.</summary>
    /// <param name="observer">The typed producer failure observer.</param>
    /// <returns>Cleanup releasing the writer and queued captures.</returns>
    /// <exception cref="InvalidOperationException">This receipt already has an independent failure observer.</exception>
    internal IDisposable SubscribeFailure(IObserver<ValidationInvalidation> observer)
    {
        ObjectDisposedException.ThrowIf(Origin.IsDisposed, this);
        if (_failureObserver is not null)
        {
            throw new InvalidOperationException("The writer receipt already has an owned failure observer.");
        }

        _failureObserver = observer;
        return Disposable.Create(this, static receipt => receipt.Dispose());
    }

    /// <summary>Delivers a queued producer error to the owning binding before draining-scope propagation.</summary>
    /// <param name="error">The original queued operation failure.</param>
    private void Fail(Exception error)
    {
        var observer = _failureObserver;
        _failureObserver = null;
        observer?.OnError(error);
    }
}
