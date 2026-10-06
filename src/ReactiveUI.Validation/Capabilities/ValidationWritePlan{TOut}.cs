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

/// <summary>Owns typed assignment observation, retaining output while targets are missing.</summary>
/// <typeparam name="TOut">The assigned value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationWritePlan")]
public sealed class ValidationWritePlan<TOut>
{
    /// <summary>The borrowed bound access factory.</summary>
    /// <summary>The bound _access contract.</summary>
    private readonly ValidationAccessPlan<ValidationTargetAccess<TOut>>? _access;

    /// <summary>The optional factory for independently owned subscription plans.</summary>
    private readonly Func<ValidationWritePlan<TOut>>? _create;

    /// <summary>Initializes a new instance of the <see cref="ValidationWritePlan{TOut}"/> class.</summary>
    /// <param name="resolve">Gets a current stable target and typed assignment.</param>
    /// <param name="dependencies">Notifications that can replace the target or assignment arguments.</param>
    public ValidationWritePlan(Func<ValidationTargetAccess<TOut>> resolve, IEnumerable<ValidationDependency> dependencies)
    {
        ArgumentExceptionHelper.ThrowIfNull(resolve);
        _access = new(
            () => ValidationRead<ValidationTargetAccess<TOut>>.Present(resolve(), []),
            dependencies,
            new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<ValidationTargetAccess<TOut>>.Default, true));
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationWritePlan{TOut}"/> class with independently owned subscription state.</summary>
    /// <param name="create">Creates a fresh bound plan for each binding subscription.</param>
    public ValidationWritePlan(Func<ValidationWritePlan<TOut>> create)
    {
        ArgumentExceptionHelper.ThrowIfNull(create);
        _create = create;
    }

    /// <summary>Binds values to freshly resolved targets, replaying the latest value after notified missing or replaced targets.</summary>
    /// <param name="values">The borrowed output stream.</param>
    /// <returns>A binding owning only subscriptions and assignment callbacks.</returns>
    /// <exception cref="InvalidOperationException">The subscription factory returned null or this same plan.</exception>
    /// <remarks>
    /// Every output refreshes the target and its dependency owners before assignment.
    /// A silent target replacement is followed on the next output; immediate replay requires a declared invalidation adapter.
    /// </remarks>
    public IValidationBinding Bind(IObservable<TOut> values)
    {
        ArgumentExceptionHelper.ThrowIfNull(values);
        if (_create is { } create)
        {
            var plan = create() ?? throw new InvalidOperationException("The write-plan factory returned null.");
            if (ReferenceEquals(plan, this))
            {
                throw new InvalidOperationException("The write-plan factory returned itself.");
            }

            return plan.Bind(values);
        }

        var binding = new Binding();
        binding.Start(_access!.Observe(binding.CreateRefreshDependency()), values);
        return binding;
    }

    /// <summary>Coordinates serialized target changes and complete cached output without retaining disposed captures.</summary>
    private sealed class Binding : IValidationBinding
    {
        /// <summary>The current access snapshot.</summary>
        private ValidationTargetAccess<TOut> _current;

        /// <summary>The last assigned owner identity.</summary>
        private object? _assignedOwner;

        /// <summary>The last assigned slot identity.</summary>
        private ValidationPath? _assignedSlot;

        /// <summary>The most recent output.</summary>
        private TOut _value = default!;

        /// <summary>The owned access subscription.</summary>
        private IDisposable? _accessSubscription;

        /// <summary>The owned value subscription.</summary>
        private IDisposable? _valueSubscription;

        /// <summary>The observation engine's independently owned output-refresh adapter.</summary>
        private IObserver<ValidationInvalidation>? _refresh;

        /// <summary>Whether an output has arrived.</summary>
        private bool _hasValue;

        /// <summary>Whether an assignment has occurred.</summary>
        private bool _hasAssignment;

        /// <summary>Whether callbacks are being drained.</summary>
        private bool _draining;

        /// <summary>Whether a callback requires another reconciliation.</summary>
        private bool _pending;

        /// <summary>Whether a new output requires an assignment even when equal.</summary>
        private bool _force;

        /// <summary>Whether this binding has ended.</summary>
        private int _disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _current = default;
            _assignedOwner = null;
            _assignedSlot = null;
            _value = default!;
            _refresh = null;
            var access = Interlocked.Exchange(ref _accessSubscription, null);
            var values = Interlocked.Exchange(ref _valueSubscription, null);
            ReleaseTokens(access, values);
        }

        /// <summary>Admits both subscriptions with rollback on synchronous failures.</summary>
        /// <param name="access">The current target stream.</param>
        /// <param name="values">The borrowed output stream.</param>
        internal void Start(IObservable<ValidationRead<ValidationTargetAccess<TOut>>> access, IObservable<TOut> values)
        {
            try
            {
                Admit(access.Subscribe(new AccessObserver(this)), true);
                if (Volatile.Read(ref _disposed) == 0)
                {
                    Admit(values.Subscribe(new ValueObserver(this)), false);
                }
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        /// <summary>Creates one private refresh source for this binding's output admission.</summary>
        /// <returns>The adapter owned by the access observation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValidationDependency CreateRefreshDependency() =>
            ValidationDependency.Create(() => this, static (owner, observer) => owner.SubscribeRefresh(observer));

        /// <summary>Attempts both owned cleanups and retains every failure.</summary>
        /// <param name="access">The access subscription.</param>
        /// <param name="values">The output subscription.</param>
        /// <exception cref="AggregateException">Both subscription cleanups fail.</exception>
        private static void ReleaseTokens(IDisposable? access, IDisposable? values)
        {
            List<Exception> errors = [];
            ReadOnlySpan<IDisposable?> tokens = [access, values];
            foreach (var token in tokens)
            {
                try
                {
                    token?.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (errors.Count == 1)
            {
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            }

            if (errors.Count > 1)
            {
                throw new AggregateException("Validation binding cleanup failed.", errors);
            }
        }

        /// <summary>Admits the observer that refreshes the same atomic access engine on each output.</summary>
        /// <param name="observer">The access engine's invalidation observer.</param>
        /// <returns>Cleanup clearing the private refresh capture.</returns>
        private IDisposable SubscribeRefresh(IObserver<ValidationInvalidation> observer)
        {
            _refresh = observer;
            return Disposable.Create(this, static owner => owner._refresh = null);
        }

        /// <summary>Captures a subscription or releases it after reentrant disposal.</summary>
        /// <param name="token">The subscription returned by a producer.</param>
        /// <param name="access">Whether the token observes access.</param>
        private void Admit(IDisposable token, bool access)
        {
            ArgumentExceptionHelper.ThrowIfNull(token);
            if (Volatile.Read(ref _disposed) != 0)
            {
                token.Dispose();
            }
            else if (access)
            {
                Volatile.Write(ref _accessSubscription, token);
            }
            else
            {
                Volatile.Write(ref _valueSubscription, token);
            }
        }

        /// <summary>Drains invalidations while always adopting fresh assignment delegates.</summary>
        private void Reconcile()
        {
            _pending = true;
            if (_draining || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _draining = true;
            try
            {
                while (_pending && Volatile.Read(ref _disposed) == 0)
                {
                    AssignCurrent();
                }
            }
            catch (Exception error)
            {
                Fail(error);
            }
            finally
            {
                _draining = false;
            }
        }

        /// <summary>Assigns one current output and detects storage changes during assignment.</summary>
        private void AssignCurrent()
        {
            _pending = false;
            var force = _force;
            _force = false;
            var access = _current;
            if (!_hasValue || !access.IsAvailable)
            {
                _hasAssignment = false;
                _assignedOwner = null;
                _assignedSlot = null;
                return;
            }

            if (!RequiresAssignment(access, force))
            {
                return;
            }

            if (Volatile.Read(ref _disposed) != 0 || _pending || _force)
            {
                _force |= force;
                return;
            }

            _hasAssignment = true;
            _assignedOwner = access.Identity;
            _assignedSlot = access.SlotIdentity;
            access.Assign(_value);
        }

        /// <summary>Checks complete owner and slot identity and optional current storage.</summary>
        /// <param name="access">The freshly resolved assignment.</param>
        /// <param name="force">Whether a new output requires assignment.</param>
        /// <returns>Whether the cached output must be applied.</returns>
        private bool RequiresAssignment(in ValidationTargetAccess<TOut> access, bool force)
        {
            if (force)
            {
                return true;
            }

            var same = _hasAssignment && ReferenceEquals(_assignedOwner, access.Identity)
                && EqualityComparer<ValidationPath?>.Default.Equals(_assignedSlot, access.SlotIdentity);
            return !same || (access.IsCurrent is not null && !access.IsCurrent(_value));
        }

        /// <summary>Releases both subscriptions before propagating the original failure.</summary>
        /// <param name="error">The producer or assignment failure.</param>
        /// <exception cref="AggregateException">Both the operation and subscription cleanup fail.</exception>
        private void Fail(Exception error)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Validation assignment and cleanup both failed.", error, cleanup);
            }

            ExceptionDispatchInfo.Capture(error).Throw();
        }

        /// <summary>Adopts current access snapshots without owning their receivers.</summary>
        /// <param name="owner">The borrowed binding.</param>
        private sealed class AccessObserver(Binding owner) : IObserver<ValidationRead<ValidationTargetAccess<TOut>>>
        {
            /// <inheritdoc/>
            public void OnNext(ValidationRead<ValidationTargetAccess<TOut>> value)
            {
                if (Volatile.Read(ref owner._disposed) != 0)
                {
                    return;
                }

                owner._current = value.Value;
                owner.Reconcile();
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnError(Exception error) => owner.Fail(error);

            /// <inheritdoc/>
            public void OnCompleted()
            {
            }
        }

        /// <summary>Caches each complete output and requests assignment to the current target.</summary>
        /// <param name="owner">The borrowed binding.</param>
        private sealed class ValueObserver(Binding owner) : IObserver<TOut>
        {
            /// <inheritdoc/>
            public void OnNext(TOut value)
            {
                if (Volatile.Read(ref owner._disposed) != 0)
                {
                    return;
                }

                owner._value = value;
                owner._hasValue = true;
                owner._force = true;
                owner._refresh?.OnNext(default);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnError(Exception error) => owner.Fail(error);

            /// <inheritdoc/>
            public void OnCompleted()
            {
            }
        }
    }
}
