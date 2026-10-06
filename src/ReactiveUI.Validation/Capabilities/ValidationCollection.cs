// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Tracks owned row subscriptions by stable keys while following whole-source replacement.</summary>
public static class ValidationCollection
{
    /// <summary>Observes snapshots and row edits without recreating unchanged row validation.</summary>
    /// <typeparam name="TKey">The stable row key.</typeparam>
    /// <typeparam name="TRow">The borrowed reference row.</typeparam>
    /// <param name="sources">The outer selection of snapshot streams; null selects an empty collection.</param>
    /// <param name="keySelector">The stable key selector.</param>
    /// <param name="validate">Creates one owned validation lease for each new row identity.</param>
    /// <param name="aggregate">Projects states in snapshot order, including the empty collection.</param>
    /// <param name="pendingState">The row state used before its first validation result.</param>
    /// <returns>A cold aggregate-state stream owning subscriptions and explicitly transferred row lifetimes.</returns>
    /// <remarks>
    /// Snapshot and row notifications must run on the same model owner. Use explicit scheduling adapters for asynchronous sources.
    /// Same-key reference replacement, removal, clear, whole-source replacement, snapshot completion, error and disposal end old row leases.
    /// A completed row releases its subscription and owned helper while retaining its last state until membership changes.
    /// Borrowed rows, sources and contexts are never disposed. Duplicate keys fail with cleanup rather than silently merge identities.
    /// A row-stream failure terminates this subscription; use a row error-to-state policy when failures should remain advisory.
    /// </remarks>
    public static IObservable<IValidationState> Observe<TKey, TRow>(
        IObservable<IObservable<IReadOnlyCollection<TRow>>?> sources,
        Func<TRow, TKey> keySelector,
        Func<TRow, ValidationRowLease> validate,
        Func<IReadOnlyList<IValidationState>, IValidationState> aggregate,
        IValidationState pendingState)
        where TKey : notnull
        where TRow : class
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);
        ArgumentExceptionHelper.ThrowIfNull(validate);
        ArgumentExceptionHelper.ThrowIfNull(aggregate);
        ArgumentExceptionHelper.ThrowIfNull(pendingState);
        return new ValidationOwnedObservable<IValidationState>(sources.Select(source =>
            new ValidationOwnedObservable<IValidationState>(new RowsObservable<TKey, TRow>(source, keySelector, validate, aggregate, pendingState))).SwitchTo());
    }

    /// <summary>Creates an independent owned row set for each subscriber.</summary>
    /// <typeparam name="TKey">The stable row key.</typeparam>
    /// <typeparam name="TRow">The borrowed row.</typeparam>
    /// <param name="source">The selected snapshot source.</param>
    /// <param name="keySelector">The row key selector.</param>
    /// <param name="validate">The owned row factory.</param>
    /// <param name="aggregate">The aggregate projection.</param>
    /// <param name="pendingState">The initial row state.</param>
    private sealed class RowsObservable<TKey, TRow>(
        IObservable<IReadOnlyCollection<TRow>>? source,
        Func<TRow, TKey> keySelector,
        Func<TRow, ValidationRowLease> validate,
        Func<IReadOnlyList<IValidationState>, IValidationState> aggregate,
        IValidationState pendingState) : IObservable<IValidationState>
        where TKey : notnull
        where TRow : class
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<IValidationState> observer)
        {
            ArgumentExceptionHelper.ThrowIfNull(observer);
            var subscription = new RowsSubscription<TKey, TRow>(keySelector, validate, aggregate, pendingState, observer);
            subscription.Start(source);
            return subscription;
        }
    }

    /// <summary>Owns row leases and drains reentrant membership and row notifications on the model owner.</summary>
    /// <typeparam name="TKey">The stable row key.</typeparam>
    /// <typeparam name="TRow">The borrowed row.</typeparam>
    /// <param name="keySelector">The row key selector.</param>
    /// <param name="validate">The owned row factory.</param>
    /// <param name="aggregate">The aggregate projection.</param>
    /// <param name="pendingState">The initial row state.</param>
    /// <param name="observer">The aggregate receiver.</param>
    [SuppressMessage("Concurrency", "PSH1306:Use atomic latch", Justification = "All notifications and disposal run on the documented model owner; the drain serializes reentry.")]
    private sealed class RowsSubscription<TKey, TRow>(
        Func<TRow, TKey> keySelector,
        Func<TRow, ValidationRowLease> validate,
        Func<IReadOnlyList<IValidationState>, IValidationState> aggregate,
        IValidationState pendingState,
        IObserver<IValidationState> observer) : IObserver<IReadOnlyCollection<TRow>>, IDisposable
        where TKey : notnull
        where TRow : class
    {
        /// <summary>The currently admitted row identities.</summary>
        private readonly Dictionary<TKey, Row> _rows = [];

        /// <summary>The reentrant immutable membership snapshots.</summary>
        private readonly Queue<TRow[]> _snapshots = [];

        /// <summary>The borrowed key selector, cleared before owned cleanup.</summary>
        private Func<TRow, TKey>? _keySelector = keySelector;

        /// <summary>The borrowed row factory, cleared before owned cleanup.</summary>
        private Func<TRow, ValidationRowLease>? _validate = validate;

        /// <summary>The borrowed aggregate projection, cleared before owned cleanup.</summary>
        private Func<IReadOnlyList<IValidationState>, IValidationState>? _aggregate = aggregate;

        /// <summary>The borrowed pending-state policy, cleared before owned cleanup.</summary>
        private IValidationState? _pendingState = pendingState;

        /// <summary>The borrowed aggregate observer, cleared before owned cleanup.</summary>
        private IObserver<IValidationState>? _observer = observer;

        /// <summary>The current snapshot order.</summary>
        private TKey[] _order = [];

        /// <summary>The borrowed snapshot subscription owned by this adapter.</summary>
        private IDisposable? _source;

        /// <summary>Whether this subscription has ended.</summary>
        private bool _disposed;

        /// <summary>Whether a membership or publication drain is active.</summary>
        private bool _draining;

        /// <summary>Whether another aggregate publication was requested.</summary>
        private bool _publishPending;

        /// <summary>Whether the snapshot source supplied an initial snapshot synchronously.</summary>
        private bool _hasSnapshot;

        /// <inheritdoc/>
        public void OnNext(IReadOnlyCollection<TRow> value)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                ArgumentExceptionHelper.ThrowIfNull(value);
                var snapshot = new TRow[value.Count];
                var index = 0;
                foreach (var row in value)
                {
                    snapshot[index] = row;
                    index++;
                }

                if (_disposed)
                {
                    return;
                }

                _hasSnapshot = true;
                _snapshots.Enqueue(snapshot);
            }
            catch (Exception error)
            {
                OnError(error);
                return;
            }

            Drain();
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            if (_disposed)
            {
                return;
            }

            var receiver = _observer;
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                error = new AggregateException("Collection validation and cleanup both failed.", error, cleanupError);
            }

            receiver?.OnError(error);
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            if (_disposed)
            {
                return;
            }

            var receiver = _observer;
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                receiver?.OnError(cleanupError);
                return;
            }

            receiver?.OnCompleted();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _keySelector = null;
            _validate = null;
            _aggregate = null;
            _pendingState = null;
            _observer = null;
            _publishPending = false;
            _snapshots.Clear();
            var rows = new Row[_rows.Count];
            _rows.Values.CopyTo(rows, 0);
            _rows.Clear();
            _order = [];
            var source = _source;
            _source = null;
            List<Exception> errors = [];
            DisposeOwned(source, errors);
            foreach (var row in rows)
            {
                DisposeOwned(row, errors);
            }

            ThrowCleanup(errors);
        }

        /// <summary>Attaches the selected source, including synchronous terminal and failed-subscription handoffs.</summary>
        /// <param name="source">The borrowed snapshot source.</param>
        internal void Start(IObservable<IReadOnlyCollection<TRow>>? source)
        {
            try
            {
                if (source is not null)
                {
                    var subscription = source.Subscribe(this);
                    if (_disposed)
                    {
                        subscription.Dispose();
                    }
                    else
                    {
                        _source = subscription;
                    }
                }

                if (!_hasSnapshot && !_disposed)
                {
                    _publishPending = true;
                    Drain();
                }

                if (source is null)
                {
                    OnCompleted();
                }
            }
            catch (Exception error)
            {
                CleanupAndRethrow(error);
                throw;
            }
        }

        /// <summary>Attempts one owned cleanup while retaining failures for remaining cleanup.</summary>
        /// <param name="owned">The owned lifetime.</param>
        /// <param name="errors">The accumulated failures.</param>
        private static void DisposeOwned(IDisposable? owned, List<Exception> errors)
        {
            try
            {
                owned?.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        /// <summary>Reports failures only after every requested lifetime has been ended.</summary>
        /// <param name="errors">The accumulated failures.</param>
        /// <exception cref="AggregateException">One or more owned lifetimes fail to dispose.</exception>
        private static void ThrowCleanup(List<Exception> errors)
        {
            if (errors.Count > 0)
            {
                throw new AggregateException("Collection validation owned cleanup failed.", errors);
            }
        }

        /// <summary>Reconciles all queued membership changes before publishing the current aggregate.</summary>
        /// <exception cref="InvalidOperationException">The aggregate returns null.</exception>
        private void Drain()
        {
            if (_draining || _disposed)
            {
                return;
            }

            _draining = true;
            try
            {
                while (!_disposed && (_snapshots.Count > 0 || _publishPending))
                {
                    if (_snapshots.TryDequeue(out var snapshot))
                    {
                        Reconcile(snapshot);
                    }
                    else
                    {
                        _publishPending = false;
                        var states = new IValidationState[_order.Length];
                        for (var index = 0; index < _order.Length; index++)
                        {
                            states[index] = _rows[_order[index]].State;
                        }

                        Publish(states);
                    }
                }
            }
            catch (Exception error) when (!_disposed)
            {
                OnError(error);
            }
            finally
            {
                _draining = false;
            }
        }

        /// <summary>Runs caller aggregation before delivery and preserves downstream callback failures.</summary>
        /// <param name="states">The current row-state snapshot.</param>
        /// <exception cref="InvalidOperationException">The aggregate returns null.</exception>
        private void Publish(IValidationState[] states)
        {
            var project = _aggregate;
            if (_disposed || project is null)
            {
                return;
            }

            var state = project(states) ?? throw new InvalidOperationException("The collection aggregate returned null.");
            if (_disposed)
            {
                return;
            }

            try
            {
                _observer?.OnNext(state);
            }
            catch (Exception error)
            {
                CleanupAndRethrow(error);
                throw;
            }
        }

        /// <summary>Captures stable keys before changing membership and rejects disposal from caller selection.</summary>
        /// <param name="snapshot">The immutable membership snapshot.</param>
        /// <returns>The selected identities, or null when caller selection ends this lifetime.</returns>
        /// <exception cref="ArgumentException">The snapshot contains duplicate keys.</exception>
        private Dictionary<TKey, TRow>? CaptureSnapshot(TRow[] snapshot)
        {
            Dictionary<TKey, TRow> next = [];
            foreach (var value in snapshot)
            {
                if (_disposed)
                {
                    return null;
                }

                ArgumentExceptionHelper.ThrowIfNull(value);
                var key = _keySelector!(value);
                if (_disposed)
                {
                    return null;
                }

                if (!next.TryAdd(key, value))
                {
                    throw new ArgumentException("Collection validation snapshots must contain unique row keys.", nameof(snapshot));
                }
            }

            return _disposed ? null : next;
        }

        /// <summary>Retains unchanged row references and replaces only removed or changed identities.</summary>
        /// <param name="snapshot">The immutable membership snapshot.</param>
        /// <exception cref="ArgumentException">The snapshot contains duplicate keys.</exception>
        /// <exception cref="InvalidOperationException">The row factory returns null.</exception>
        private void Reconcile(TRow[] snapshot)
        {
            var next = CaptureSnapshot(snapshot);
            if (next is null)
            {
                return;
            }

            List<Exception> errors = [];
            List<TKey> removed = [];
            foreach (var (key, row) in _rows)
            {
                var retained = next.TryGetValue(key, out var value) && ReferenceEquals(value, row.Value);
                if (_disposed)
                {
                    return;
                }

                if (!retained)
                {
                    removed.Add(key);
                }
            }

            foreach (var key in removed)
            {
                if (_rows.Remove(key, out var row))
                {
                    DisposeOwned(row, errors);
                }
            }

            ThrowCleanup(errors);
            if (_disposed)
            {
                return;
            }

            _order = new TKey[next.Count];
            next.Keys.CopyTo(_order, 0);
            foreach (var (key, value) in next)
            {
                AdmitRow(key, value);
            }

            _publishPending = !_disposed;
        }

        /// <summary>Admits one newly selected row and safely attaches its synchronous subscription.</summary>
        /// <param name="key">The stable row key.</param>
        /// <param name="value">The borrowed row.</param>
        /// <exception cref="InvalidOperationException">The row factory returns null.</exception>
        private void AdmitRow(TKey key, TRow value)
        {
            if (_disposed || _rows.ContainsKey(key))
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            var initialState = _pendingState!;
            var lease = _validate!(value) ?? throw new InvalidOperationException("The row validation factory returned null.");
            var row = new Row(value, lease, initialState);
            if (_disposed)
            {
                row.Dispose();
                return;
            }

            _rows.Add(key, row);
            if (_disposed)
            {
                _rows.Clear();
                row.Dispose();
                return;
            }

            row.Attach(new ValidationOwnedObservable<IValidationState>(lease.States).Subscribe(new RowObserver(this, key, row)));
        }

        /// <summary>Rejects stale row callbacks and queues a complete current-state projection.</summary>
        /// <param name="key">The original row key.</param>
        /// <param name="row">The original owned row.</param>
        /// <param name="state">The complete row state.</param>
        private void RowChanged(TKey key, Row row, IValidationState state)
        {
            if (_disposed || !row.IsActive || !_rows.TryGetValue(key, out var current) || !ReferenceEquals(current, row))
            {
                return;
            }

            if (_disposed || !row.IsActive)
            {
                return;
            }

            ArgumentExceptionHelper.ThrowIfNull(state);
            row.State = state;
            _publishPending = true;
            Drain();
        }

        /// <summary>Preserves the original operation failure if owned cleanup also fails.</summary>
        /// <param name="error">The original failure.</param>
        /// <exception cref="AggregateException">The original operation and owned cleanup both fail.</exception>
        private void CleanupAndRethrow(Exception error)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Collection validation and cleanup both failed.", error, cleanupError);
            }
        }

        /// <summary>Owns a single row's subscription and explicit helper lease.</summary>
        /// <param name="value">The borrowed row identity.</param>
        /// <param name="lease">The transferred row lease.</param>
        /// <param name="initialState">The pending row policy.</param>
        private sealed class Row(TRow value, ValidationRowLease lease, IValidationState initialState) : IDisposable
        {
            /// <summary>The owned row subscription, assigned after synchronous initial notifications.</summary>
            private IDisposable? _subscription;

            /// <summary>Whether the row has left the current membership.</summary>
            private bool _disposed;

            /// <summary>Gets the borrowed row identity.</summary>
            internal TRow Value { get; } = value;

            /// <summary>Gets or sets the complete current row state.</summary>
            internal IValidationState State { get; set; } = initialState;

            /// <summary>Gets whether the row can still deliver state notifications.</summary>
            internal bool IsActive => !_disposed;

            /// <inheritdoc/>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                var subscription = _subscription;
                _subscription = null;
                List<Exception> errors = [];
                DisposeOwned(subscription, errors);
                DisposeOwned(lease, errors);
                ThrowCleanup(errors);
            }

            /// <summary>Handles replacement or terminal callbacks occurring before subscription returns.</summary>
            /// <param name="subscription">The freshly returned row subscription.</param>
            internal void Attach(IDisposable subscription)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                }
                else
                {
                    _subscription = subscription;
                }
            }
        }

        /// <summary>Routes callbacks through the current row identity check.</summary>
        /// <param name="owner">The current aggregate subscription.</param>
        /// <param name="key">The original stable key.</param>
        /// <param name="row">The original owned row.</param>
        private sealed class RowObserver(RowsSubscription<TKey, TRow> owner, TKey key, Row row) : IObserver<IValidationState>
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnNext(IValidationState value) => owner.RowChanged(key, row, value);

            /// <inheritdoc/>
            public void OnError(Exception error)
            {
                if (row.IsActive && owner._rows.TryGetValue(key, out var current) && ReferenceEquals(current, row))
                {
                    owner.OnError(error);
                }
            }

            /// <inheritdoc/>
            public void OnCompleted()
            {
                if (!row.IsActive || !owner._rows.TryGetValue(key, out var current) || !ReferenceEquals(current, row))
                {
                    return;
                }

                try
                {
                    row.Dispose();
                }
                catch (Exception error)
                {
                    owner.OnError(error);
                }
            }
        }
    }
}
