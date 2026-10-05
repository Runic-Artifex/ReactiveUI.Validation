// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Preserves formatter selection and property-message projection for generated bindings.</summary>
public static class GeneratedValidationBindingSupport
{
    /// <summary>Uses a supplied formatter or resolves the registered default with the ordinary fallback.</summary>
    /// <param name="formatter">An optional caller-supplied formatter.</param>
    /// <returns>The selected formatter.</returns>
    public static IValidationTextFormatter<string> ResolveFormatter(IValidationTextFormatter<string>? formatter) =>
        formatter ?? ValidationTextFormatterResolver.Resolve();

    /// <summary>Applies projected validation values to the current typed target and replays them after target replacement.</summary>
    /// <typeparam name="TTarget">The target owner type.</typeparam>
    /// <typeparam name="TOut">The projected value type.</typeparam>
    /// <param name="targets">Emits the initial target and every replacement, including null.</param>
    /// <param name="bind">Creates the validation binding using the supplied value callback.</param>
    /// <param name="setter">Assigns the value through ordinary typed member access.</param>
    /// <returns>A binding owning both the target observation and validation subscriptions.</returns>
    /// <exception cref="AggregateException">Subscription and owned cleanup both fail.</exception>
    /// <remarks>
    /// Null targets skip assignment and retain the latest value for a later non-null target. Callbacks run synchronously.
    /// Caller-supplied observables and binding factories must clean up their own registration if they throw before returning a subscription.
    /// </remarks>
    public static IValidationBinding BindToTarget<TTarget, TOut>(IObservable<TTarget?> targets, Func<Action<TOut>, IValidationBinding> bind, Action<TTarget, TOut> setter)
        where TTarget : class
    {
        ArgumentExceptionHelper.ThrowIfNull(targets);
        ArgumentExceptionHelper.ThrowIfNull(bind);
        ArgumentExceptionHelper.ThrowIfNull(setter);
        var binding = new TargetBinding<TTarget, TOut>(setter);
        try
        {
            binding.AttachTargets(targets);
            binding.AttachValidation(bind);
            return binding;
        }
        catch (Exception subscriptionError)
        {
            try
            {
                binding.Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Generated binding subscription and cleanup both failed.", subscriptionError, cleanupError);
            }

            throw;
        }
    }

    /// <summary>Formats the first nonempty message, or an empty string when every state formats empty.</summary>
    /// <param name="states">The current matching rule states.</param>
    /// <param name="formatter">The selected formatter.</param>
    /// <returns>The first nonempty formatted message.</returns>
    public static string FirstNonEmptyMessage(IList<IValidationState> states, IValidationTextFormatter<string> formatter)
    {
        ArgumentExceptionHelper.ThrowIfNull(states);
        ArgumentExceptionHelper.ThrowIfNull(formatter);
        foreach (var state in states)
        {
            var message = formatter.Format(state.Text);
            if (!string.IsNullOrEmpty(message))
            {
                return message;
            }
        }

        return string.Empty;
    }

    /// <summary>Owns parent and validation subscriptions, replaying the current projected value after parent replacement.</summary>
    /// <typeparam name="TTarget">The typed target owner.</typeparam>
    /// <typeparam name="TOut">The projected value type.</typeparam>
    /// <param name="setter">The typed property assignment.</param>
    [SuppressMessage("Concurrency", "PSH1306:Use atomic latch", Justification = "Observation and disposal run on the source owner thread; these guards serialize reentrant callbacks.")]
    private sealed class TargetBinding<TTarget, TOut>(Action<TTarget, TOut> setter) : IValidationBinding, IObserver<TTarget?>
        where TTarget : class
    {
        /// <summary>The parent observation ownership slot, installed before subscription.</summary>
        /// <summary>The latest target, or null.</summary>
        private readonly OwnedSubscription _targets = new();

        /// <summary>The validation ownership slot, installed before invoking the binding factory.</summary>
        private readonly OwnedSubscription _validation = new();

        /// <summary>The latest target, or null.</summary>
        private TTarget? _target;

        /// <summary>The latest projected value.</summary>
        private TOut? _value;

        /// <summary>Whether any projected value has arrived.</summary>
        private bool _hasValue;

        /// <summary>Whether the binding has ended.</summary>
        private bool _disposed;

        /// <summary>Whether reentrant assignments are currently draining.</summary>
        private bool _applying;

        /// <summary>Whether a callback requested another assignment.</summary>
        [SuppressMessage("Style", "SST1422:Move field into method", Justification = "This flag must survive reentrant calls to Apply while the outer call drains assignments.")]
        private bool _pending;

        /// <inheritdoc/>
        [SuppressMessage("Design", "SST1485:Dispose should not throw", Justification = "Both owned sources must be attempted and their real disposal failures preserved for the caller.")]
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _target = null;
            _value = default;
            Exception? validationError = null;
            try
            {
                _validation.Dispose();
            }
            catch (Exception error)
            {
                validationError = error;
            }

            try
            {
                _targets.Dispose();
            }
            catch (Exception targetError) when (validationError is not null)
            {
                throw new AggregateException("Generated binding subscription cleanup failed.", validationError, targetError);
            }

            if (validationError is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(validationError).Throw();
            }
        }

        /// <inheritdoc/>
        public void OnNext(TTarget? value)
        {
            if (_disposed)
            {
                return;
            }

            _target = value;
            Apply();
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Generated target observation and cleanup both failed.", error, cleanupError);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        /// <summary>Assigns the parent observation token after synchronous initial delivery returns.</summary>
        /// <param name="targets">The parent stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AttachTargets(IObservable<TTarget?> targets) => _targets.Assign(targets.Subscribe(this));

        /// <summary>Assigns the validation token after synchronous factory delivery returns.</summary>
        /// <param name="bind">The binding factory.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AttachValidation(Func<Action<TOut>, IValidationBinding> bind) => _validation.Assign(bind(SetValue));

        /// <summary>Caches each projected value and replays it to the current target.</summary>
        /// <param name="value">The projected value.</param>
        private void SetValue(TOut value)
        {
            if (_disposed)
            {
                return;
            }

            _value = value;
            _hasValue = true;
            Apply();
        }

        /// <summary>Drains reentrant target and value changes, always applying the most recent pair.</summary>
        /// <exception cref="AggregateException">Assignment and owned cleanup both fail.</exception>
        private void Apply()
        {
            _pending = true;
            if (_applying)
            {
                return;
            }

            _applying = true;
            try
            {
                while (_pending && !_disposed)
                {
                    _pending = false;
                    if (_hasValue && _target is { } target)
                    {
                        setter(target, _value!);
                    }
                }
            }
            catch (Exception assignmentError)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("Generated binding assignment and cleanup both failed.", assignmentError, cleanupError);
                }

                throw;
            }
            finally
            {
                _applying = false;
            }
        }
    }

    /// <summary>Disposes tokens returned after their ownership slot has already been disposed.</summary>
    private sealed class OwnedSubscription : IDisposable
    {
        /// <summary>The owned subscription, once returned by the source.</summary>
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Interlocked.Exchange transfers the owned subscription to the disposer.")]
        private IDisposable? _subscription;

        /// <summary>Whether the binding has ended.</summary>
        private bool _disposed;

        /// <inheritdoc/>
        [SuppressMessage("Design", "SST1485:Dispose should not throw", Justification = "Both owned sources must be attempted and their real disposal failures preserved for the caller.")]
        public void Dispose()
        {
            _disposed = true;
            Interlocked.Exchange(ref _subscription, null)?.Dispose();
        }

        /// <summary>Stores the returned token or disposes it immediately when the slot has ended.</summary>
        /// <param name="subscription">The returned subscription token.</param>
        internal void Assign(IDisposable subscription)
        {
            if (_disposed)
            {
                subscription.Dispose();
            }
            else
            {
                Volatile.Write(ref _subscription, subscription);
            }
        }
    }
}
