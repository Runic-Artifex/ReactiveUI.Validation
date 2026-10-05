// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Observes statically selected getters and property notification chains for generated validation.</summary>
/// <remarks>Notifications run synchronously on the source thread. Getters must include generated null guards.</remarks>
public static class GeneratedValidationObservation
{
    /// <summary>Emits the current value and subsequent changed values while following replacement owners.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="getter">Gets the current value using ordinary typed member access.</param>
    /// <param name="properties">Notifications that can change the value or any owner in its path.</param>
    /// <returns>A cold observable that detaches all property handlers on disposal or subscription failure.</returns>
    /// <remarks>Empty and null notification names refresh the whole chain. Values use default equality for change detection.</remarks>
    public static IObservable<TValue> Observe<TValue>(Func<TValue> getter, params GeneratedValidationProperty[] properties)
    {
        ArgumentExceptionHelper.ThrowIfNull(getter);
        ArgumentExceptionHelper.ThrowIfNull(properties);
        foreach (var property in properties)
        {
            ArgumentExceptionHelper.ThrowIfNull(property);
        }

        return new PropertyObservable<TValue>(getter, (GeneratedValidationProperty[])properties.Clone(), EqualityComparer<TValue>.Default);
    }

    /// <summary>Observes selected object identities, emitting distinct replacement objects even when they compare equal.</summary>
    /// <typeparam name="TValue">The selected object type.</typeparam>
    /// <param name="getter">Gets the current object, or null.</param>
    /// <param name="properties">Notifications that can replace the selected object or its owners.</param>
    /// <returns>A cold identity-based observable with synchronous initial delivery.</returns>
    public static IObservable<TValue?> ObserveReference<TValue>(Func<TValue?> getter, params GeneratedValidationProperty[] properties)
        where TValue : class
    {
        ArgumentExceptionHelper.ThrowIfNull(getter);
        ArgumentExceptionHelper.ThrowIfNull(properties);
        foreach (var property in properties)
        {
            ArgumentExceptionHelper.ThrowIfNull(property);
        }

        return new PropertyObservable<TValue?>(getter, (GeneratedValidationProperty[])properties.Clone(), ReferenceEqualityComparer.Instance);
    }

    /// <summary>Creates the actionable failure used when a generated-only call reaches the runtime stub.</summary>
    /// <param name="methodName">The public method name.</param>
    /// <returns>The missing-interceptor exception.</returns>
    internal static InvalidOperationException RequiresGenerator(string methodName) =>
        new($"{methodName} requires the bundled Runic Validation generator, a compatible SDK, and an inline call. Use {methodName}Unsafe for reflection-based execution.");

    /// <summary>Creates an independent owned observation chain for each subscriber.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="getter">The typed current-value getter.</param>
    /// <param name="properties">The path notification descriptors.</param>
    /// <param name="comparer">The selected equality policy.</param>
    private sealed class PropertyObservable<TValue>(Func<TValue> getter, GeneratedValidationProperty[] properties, IEqualityComparer<TValue> comparer) : IObservable<TValue>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            ArgumentExceptionHelper.ThrowIfNull(observer);
            var subscription = new PropertySubscription<TValue>(getter, properties, observer, comparer);
            subscription.Refresh();
            return subscription;
        }
    }

    /// <summary>Owns current property handlers and drains reentrant refreshes on the source owner thread.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="getter">The typed current-value getter.</param>
    /// <param name="properties">The path notification descriptors.</param>
    /// <param name="observer">The current-value receiver.</param>
    /// <param name="comparer">The selected equality policy.</param>
    [SuppressMessage("Concurrency", "PSH1306:Use atomic latch", Justification = "Observation and disposal run on the source owner thread; these guards serialize reentrant callbacks.")]
    private sealed class PropertySubscription<TValue>(Func<TValue> getter, GeneratedValidationProperty[] properties, IObserver<TValue> observer, IEqualityComparer<TValue> comparer) : IDisposable
    {
        /// <summary>The property handlers owned by the current generation.</summary>
        private readonly List<(INotifyPropertyChanged Owner, PropertyChangedEventHandler Handler)> _handlers = [];

        /// <summary>Whether the subscription has ended.</summary>
        private bool _disposed;

        /// <summary>Whether a reentrant refresh is currently draining.</summary>
        private bool _refreshing;

        /// <summary>Whether another refresh was requested during delivery.</summary>
        private bool _pending;

        /// <summary>Whether an initial value has been delivered.</summary>
        private bool _hasValue;

        /// <summary>The identity of the currently attached handler generation.</summary>
        private long _generation;

        /// <summary>The most recently delivered value.</summary>
        private TValue? _value;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Detach();
        }

        /// <summary>Drains pending notifications after invalidating each previous handler generation.</summary>
        /// <exception cref="AggregateException">Observation and owned cleanup both fail.</exception>
        internal void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            _pending = true;
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            try
            {
                while (_pending && !_disposed)
                {
                    RefreshCurrent();
                }
            }
            catch (Exception observationError)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("Generated observation and cleanup both failed.", observationError, cleanupError);
                }

                throw;
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>Rebuilds the current path and emits a changed value after all handlers are attached.</summary>
        private void RefreshCurrent()
        {
            _pending = false;
            Detach();
            AttachOwners();
            if (_disposed || _pending)
            {
                return;
            }

            var current = getter();
            if (_disposed || _pending || (_hasValue && comparer.Equals(_value!, current)))
            {
                return;
            }

            _value = current;
            _hasValue = true;
            observer.OnNext(current);
        }

        /// <summary>Attaches every currently non-null property owner before reading the selected value.</summary>
        private void AttachOwners()
        {
            foreach (var property in properties)
            {
                if (_disposed || _pending)
                {
                    return;
                }

                var owner = property.Owner();
                if (_disposed || _pending)
                {
                    return;
                }

                if (owner is null)
                {
                    continue;
                }

                var generation = _generation;
                PropertyChangedEventHandler handler = (_, args) => HandleChange(property, generation, args);
                _handlers.Add((owner, handler));
                owner.PropertyChanged += handler;
            }
        }

        /// <summary>Ignores obsolete registrations and refreshes matching or wildcard notifications.</summary>
        /// <param name="property">The current property descriptor.</param>
        /// <param name="generation">The registration generation.</param>
        /// <param name="args">The property notification.</param>
        private void HandleChange(GeneratedValidationProperty property, long generation, PropertyChangedEventArgs args)
        {
            if (_disposed || generation != _generation)
            {
                return;
            }

            if (string.IsNullOrEmpty(args.PropertyName) || string.Equals(args.PropertyName, property.PropertyName, StringComparison.Ordinal))
            {
                Refresh();
            }
        }

        /// <summary>Invalidates every old handler and attempts every removal before reporting cleanup failure.</summary>
        /// <exception cref="AggregateException">One or more property handlers fail to detach.</exception>
        private void Detach()
        {
            _generation++;
            var handlers = _handlers.ToArray();
            _handlers.Clear();
            List<Exception>? errors = null;
            foreach (var (owner, handler) in handlers)
            {
                try
                {
                    owner.PropertyChanged -= handler;
                }
                catch (Exception error)
                {
                    (errors ??= []).Add(error);
                }
            }

            if (errors is not null)
            {
                throw new AggregateException("Generated observation handler cleanup failed.", errors);
            }
        }
    }
}
