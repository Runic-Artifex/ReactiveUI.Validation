// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Creates independently owned observation chains.</summary>
/// <typeparam name="TValue">The selected value type.</typeparam>
/// <param name="read">The typed snapshot getter.</param>
/// <param name="dependencies">The typed owner registration adapters.</param>
/// <param name="options">The observation policies.</param>
internal sealed class ValidationPlanObservable<TValue>(
    Func<ValidationRead<TValue>> read,
    ValidationDependency[] dependencies,
    ValidationObservationOptions<TValue> options) : IObservable<ValidationRead<TValue>>
{
    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<ValidationRead<TValue>> observer)
    {
        ArgumentExceptionHelper.ThrowIfNull(observer);
        var subscription = Subscription.Create(read, dependencies, options, observer);
        subscription.Refresh();
        return subscription;
    }

    /// <summary>Owns registrations and releases all caller captures on termination.</summary>
    private sealed class Subscription : IDisposable
    {
        /// <summary>The getter, cleared on termination.</summary>
        private Func<ValidationRead<TValue>>? _read;

        /// <summary>The policies, cleared on termination.</summary>
        private ValidationObservationOptions<TValue>? _options;

        /// <summary>The downstream observer, cleared on termination.</summary>
        private IObserver<ValidationRead<TValue>>? _observer;

        /// <summary>The current dependency registrations.</summary>
        private Entry[] _entries = [];

        /// <summary>The atomic termination latch.</summary>
        private int _disposed;

        /// <summary>The refresh reentrancy latch.</summary>
        private int _refreshing;

        /// <summary>Whether another owner read is requested.</summary>
        private bool _pending;

        /// <summary>Whether a snapshot has been delivered.</summary>
        private bool _hasValue;

        /// <summary>Whether an exception comes from downstream delivery.</summary>
        private bool _delivering;

        /// <summary>The last delivered snapshot.</summary>
        private ValidationRead<TValue> _value;

        /// <summary>Initializes a new instance of the <see cref="Subscription"/> class.</summary>
        /// <param name="read">The getter.</param>
        /// <param name="options">The policies.</param>
        /// <param name="observer">The downstream observer.</param>
        private Subscription(Func<ValidationRead<TValue>> read, ValidationObservationOptions<TValue> options, IObserver<ValidationRead<TValue>> observer)
        {
            _read = read;
            _options = options;
            _observer = observer;
        }

        /// <summary>Gets whether user code stopped or invalidated the current read.</summary>
        private bool Interrupted => Volatile.Read(ref _disposed) != 0 || _pending;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => ThrowCleanupFailure(Cleanup());

        /// <summary>Creates entries only after the subscription is fully constructed.</summary>
        /// <param name="read">The getter.</param>
        /// <param name="dependencies">The adapters.</param>
        /// <param name="options">The policies.</param>
        /// <param name="observer">The downstream observer.</param>
        /// <returns>The independently owned subscription.</returns>
        internal static Subscription Create(
            Func<ValidationRead<TValue>> read,
            ValidationDependency[] dependencies,
            ValidationObservationOptions<TValue> options,
            IObserver<ValidationRead<TValue>> observer)
        {
            var subscription = new Subscription(read, options, observer);
            var entries = new Entry[dependencies.Length];
            for (var index = 0; index < dependencies.Length; index++)
            {
                entries[index] = new(subscription, dependencies[index]);
            }

            subscription._entries = entries;
            return subscription;
        }

        /// <summary>Coalesces synchronous invalidations without recursive delivery.</summary>
        /// <exception cref="AggregateException">Both a downstream callback and registration cleanup failed.</exception>
        internal void Refresh()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _pending = true;
            if (Interlocked.Exchange(ref _refreshing, 1) != 0)
            {
                return;
            }

            try
            {
                while (_pending && Volatile.Read(ref _disposed) == 0)
                {
                    RefreshOnce();
                }
            }
            catch (Exception error) when (Volatile.Read(ref _disposed) == 0)
            {
                if (_delivering)
                {
                    var cleanupError = Cleanup();
                    if (cleanupError is not null)
                    {
                        throw new AggregateException("Validation callback and cleanup both failed.", error, cleanupError);
                    }

                    throw;
                }

                Fail(error);
            }
            finally
            {
                Volatile.Write(ref _refreshing, 0);
            }
        }

        /// <summary>Propagates a cleanup failure after all registrations have been attempted.</summary>
        /// <param name="error">The cleanup failure, if any.</param>
        private static void ThrowCleanupFailure(Exception? error)
        {
            if (error is not null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        /// <summary>Compares presentation metadata as well as structural path identity.</summary>
        /// <param name="previous">The previously delivered path.</param>
        /// <param name="current">The current path.</param>
        /// <returns>Whether both presentation and structural identity match.</returns>
        private static bool SamePath(ValidationPath previous, ValidationPath current) =>
            string.Equals(previous.DisplayPath, current.DisplayPath, StringComparison.Ordinal) && previous.Equals(current);

        /// <summary>Stops callbacks and releases captures before disposing registrations.</summary>
        /// <returns>An aggregate of any registration cleanup failures.</returns>
        private AggregateException? Cleanup()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return null;
            }

            var entries = _entries;
            _entries = [];
            _read = null;
            _options = null;
            _observer = null;
            _value = default;
            List<Exception>? errors = null;
            foreach (var entry in entries)
            {
                try
                {
                    entry.Release();
                }
                catch (Exception error)
                {
                    (errors ??= []).Add(error);
                }
            }

            return errors is null ? null : new AggregateException("Validation dependency cleanup failed.", errors);
        }

        /// <summary>Reads ordinary owners, then the typed snapshot and its cached branch owners.</summary>
        private void RefreshOnce()
        {
            _pending = false;
            RefreshEntries(afterRead: false);
            if (Interrupted)
            {
                return;
            }

            var current = _read!();
            if (Interrupted)
            {
                return;
            }

            RefreshEntries(afterRead: true);
            if (Interrupted)
            {
                return;
            }

            DeliverCurrent(current);
            CompleteIfFinished();
        }

        /// <summary>Refreshes only the dependencies belonging to the current observation phase.</summary>
        /// <param name="afterRead">Whether the typed snapshot has already cached its branch owners.</param>
        private void RefreshEntries(bool afterRead)
        {
            foreach (var entry in _entries)
            {
                if (Interrupted)
                {
                    return;
                }

                if (entry.RequiresAfterRead == afterRead)
                {
                    entry.RefreshOwner();
                }
            }
        }

        /// <summary>Applies missing-owner and equality policies to a fresh snapshot.</summary>
        /// <param name="current">The typed snapshot whose branch owners are already registered.</param>
        private void DeliverCurrent(ValidationRead<TValue> current)
        {
            var options = _options!;
            if (!current.HasOwner)
            {
                if (options.MissingOwner == ValidationMissingOwnerPolicy.Suppress)
                {
                    return;
                }

                current = current.WithValue(options.MissingOwner == ValidationMissingOwnerPolicy.Fallback
                    ? options.Fallback!()
                    : default!);
            }

            if (Interrupted)
            {
                return;
            }

            var equal = !options.EmitEveryInvalidation && _hasValue && Same(current, options);
            if (Interrupted || equal)
            {
                return;
            }

            _value = current;
            _hasValue = true;
            _delivering = true;
            _observer!.OnNext(current);
            _delivering = false;
        }

        /// <summary>Terminates even when the final value was suppressed or equal.</summary>
        private void CompleteIfFinished()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            foreach (var entry in _entries)
            {
                if (!entry.Completed)
                {
                    return;
                }
            }

            var observer = _observer!;
            var error = Cleanup();
            if (error is not null)
            {
                observer.OnError(error);
                return;
            }

            observer.OnCompleted();
        }

        /// <summary>Compares complete value and path snapshots.</summary>
        /// <param name="current">The current snapshot.</param>
        /// <param name="options">The value equality policies.</param>
        /// <returns>Whether the snapshots have equal owner presence, values and paths.</returns>
        private bool Same(ValidationRead<TValue> current, ValidationObservationOptions<TValue> options)
        {
            var previous = _value;
            if (previous.HasOwner != current.HasOwner || previous.Paths.Count != current.Paths.Count)
            {
                return false;
            }

            if (!options.Comparer.Equals(previous.Value, current.Value) || Interrupted)
            {
                return false;
            }

            for (var index = 0; index < current.Paths.Count; index++)
            {
                if (!SamePath(previous.Paths[index], current.Paths[index]) || Interrupted)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Notifies producer failure after releasing all owned registrations.</summary>
        /// <param name="error">The producer failure.</param>
        /// <param name="handoffError">An optional registration handoff failure.</param>
        private void Fail(Exception error, Exception? handoffError = null)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                ThrowCleanupFailure(handoffError);
                return;
            }

            var observer = _observer!;
            var cleanupError = Cleanup();
            if (handoffError is not null)
            {
                cleanupError = cleanupError is null
                    ? new AggregateException("Validation registration handoff failed.", handoffError)
                    : new AggregateException("Validation dependency cleanup failed.", handoffError, cleanupError);
            }

            observer.OnError(cleanupError is null ? error : new AggregateException("Validation observation and cleanup both failed.", error, cleanupError));
        }

        /// <summary>Tracks one exact dependency owner and rejects stale callbacks.</summary>
        private sealed class Entry
        {
            /// <summary>The owning subscription.</summary>
            private readonly Subscription _parent;

            /// <summary>The adapter, cleared on termination.</summary>
            private ValidationDependency? _dependency;

            /// <summary>The exact currently attached owner.</summary>
            private object? _owner;

            /// <summary>Whether the current provider completed.</summary>
            private bool _completed;

            /// <summary>The independently owned registration token.</summary>
            private IDisposable? _token;

            /// <summary>The callback generation.</summary>
            private long _generation;

            /// <summary>Whether a registration token has not yet returned.</summary>
            private bool _attaching;

            /// <summary>A synchronous terminal error awaiting token handoff.</summary>
            private Exception? _attachmentError;

            /// <summary>Initializes a new instance of the <see cref="Entry"/> class.</summary>
            /// <param name="parent">The owning subscription.</param>
            /// <param name="dependency">The typed adapter.</param>
            internal Entry(Subscription parent, ValidationDependency dependency)
            {
                _parent = parent;
                _dependency = dependency;
            }

            /// <summary>Gets whether the attached provider has completed.</summary>
            internal bool Completed => _completed;

            /// <summary>Gets whether this dependency follows owners cached by the typed snapshot read.</summary>
            internal bool RequiresAfterRead => _dependency!.RequiresAfterRead;

            /// <summary>Reattaches only when the dependency owner's reference changes.</summary>
            internal void RefreshOwner()
            {
                var dependency = _dependency!;
                var owner = dependency.GetOwner();
                if (Volatile.Read(ref _parent._disposed) != 0 || _parent._pending || ReferenceEquals(owner, _owner))
                {
                    return;
                }

                Detach();
                if (Volatile.Read(ref _parent._disposed) != 0 || _parent._pending || owner is null)
                {
                    return;
                }

                _owner = owner;
                _completed = false;
                Attach(dependency, owner);
            }

            /// <summary>Releases adapter captures as well as the current registration.</summary>
            internal void Release()
            {
                _dependency = null;
                Detach();
            }

            /// <summary>Defers synchronous provider errors until the registration token is owned.</summary>
            /// <param name="dependency">The current adapter.</param>
            /// <param name="owner">The exact provider owner.</param>
            private void Attach(ValidationDependency dependency, object owner)
            {
                var generation = _generation;
                IDisposable token;
                _attaching = true;
                try
                {
                    token = dependency.Subscribe(owner, new Observer(this, generation));
                    ArgumentExceptionHelper.ThrowIfNull(token);
                }
                catch (Exception error) when (_attachmentError is not null)
                {
                    var providerError = _attachmentError;
                    _attachmentError = null;
                    _parent.Fail(providerError, error);
                    return;
                }
                finally
                {
                    _attaching = false;
                }

                if (_attachmentError is { } terminalError)
                {
                    _attachmentError = null;
                    FinishFailedAttachment(token, terminalError);
                    return;
                }

                if (Volatile.Read(ref _parent._disposed) != 0 || generation != _generation)
                {
                    token.Dispose();
                    return;
                }

                Volatile.Write(ref _token, token);
            }

            /// <summary>Releases a late token before reporting a synchronous provider error.</summary>
            /// <param name="token">The returned registration token.</param>
            /// <param name="error">The original provider error.</param>
            private void FinishFailedAttachment(IDisposable token, Exception error)
            {
                Exception? cleanupError = null;
                try
                {
                    token.Dispose();
                }
                catch (Exception failure)
                {
                    cleanupError = failure;
                }

                _parent.Fail(error, cleanupError);
            }

            /// <summary>Invalidates callbacks before disposing the owned registration.</summary>
            private void Detach()
            {
                _generation++;
                _owner = null;
                Interlocked.Exchange(ref _token, null)?.Dispose();
            }

            /// <summary>Forwards only callbacks from the currently attached generation.</summary>
            /// <param name="entry">The registration entry.</param>
            /// <param name="generation">The callback generation.</param>
            private sealed class Observer(Entry entry, long generation) : IObserver<ValidationInvalidation>
            {
                /// <inheritdoc />
                public void OnNext(ValidationInvalidation value)
                {
                    if (generation == entry._generation && Volatile.Read(ref entry._parent._disposed) == 0)
                    {
                        entry._parent.Refresh();
                    }
                }

                /// <inheritdoc />
                public void OnError(Exception error)
                {
                    if (generation != entry._generation || Volatile.Read(ref entry._parent._disposed) != 0)
                    {
                        return;
                    }

                    if (entry._attaching)
                    {
                        entry._attachmentError = error;
                        entry._generation++;
                        return;
                    }

                    entry._parent.Fail(error);
                }

                /// <inheritdoc />
                public void OnCompleted()
                {
                    if (generation != entry._generation || Volatile.Read(ref entry._parent._disposed) != 0)
                    {
                        return;
                    }

                    entry._completed = true;
                    entry._generation++;
                    try
                    {
                        Interlocked.Exchange(ref entry._token, null)?.Dispose();
                    }
                    catch (Exception error)
                    {
                        entry._parent.Fail(error);
                        return;
                    }

                    if (Volatile.Read(ref entry._parent._refreshing) == 0)
                    {
                        entry._parent.CompleteIfFinished();
                    }
                }
            }
        }
    }
}
