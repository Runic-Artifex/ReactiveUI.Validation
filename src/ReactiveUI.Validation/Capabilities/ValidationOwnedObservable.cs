// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Ends every upstream lifetime on terminal delivery or callback failure, including synchronous handoff.</summary>
/// <typeparam name="TValue">The delivered value.</typeparam>
/// <param name="source">The borrowed source pipeline.</param>
internal sealed class ValidationOwnedObservable<TValue>(IObservable<TValue> source) : IObservable<TValue>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<TValue> observer)
    {
        ArgumentExceptionHelper.ThrowIfNull(observer);
        var subscription = new OwnedSubscription(observer);
        subscription.Start(source);
        return subscription;
    }

    /// <summary>Owns the complete pipeline and rejects stale or post-terminal callbacks.</summary>
    /// <param name="observer">The borrowed receiver.</param>
    [SuppressMessage("Concurrency", "PSH1306:Use atomic latch", Justification = "Adapter callbacks and disposal run on the explicitly selected owner; the flag supports synchronous handoff.")]
    private sealed class OwnedSubscription(IObserver<TValue> observer) : IObserver<TValue>, IDisposable
    {
        /// <summary>The borrowed receiver, cleared before owned cleanup.</summary>
        private IObserver<TValue>? _observer = observer;

        /// <summary>The owned upstream pipeline.</summary>
        private IDisposable? _upstream;

        /// <summary>Whether this lifetime has ended.</summary>
        private bool _disposed;

        /// <summary>Whether synchronous subscription handoff is still in progress.</summary>
        private bool _starting;

        /// <summary>The original synchronous callback failure, before returned-token cleanup.</summary>
        private Exception? _initialFailure;

        /// <summary>The synchronous terminal error awaiting returned-token cleanup.</summary>
        private Exception? _pendingError;

        /// <summary>Whether synchronous completion awaits returned-token cleanup.</summary>
        private bool _pendingCompletion;

        /// <inheritdoc/>
        public void OnNext(TValue value)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _observer?.OnNext(value);
            }
            catch (Exception error)
            {
                if (_starting)
                {
                    _initialFailure = error;
                }

                CleanupAfterFailure(error);
                throw;
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            if (_disposed)
            {
                return;
            }

            if (_starting)
            {
                _disposed = true;
                _pendingError = error;
                return;
            }

            var receiver = _observer;
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                error = new AggregateException("Validation adapter and upstream cleanup both failed.", error, cleanupError);
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

            if (_starting)
            {
                _disposed = true;
                _pendingCompletion = true;
                return;
            }

            var receiver = _observer;
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                receiver?.OnError(error);
                return;
            }

            receiver?.OnCompleted();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _disposed = true;
            _observer = null;
            var upstream = _upstream;
            _upstream = null;
            upstream?.Dispose();
        }

        /// <summary>Attaches a returned upstream subscription after any synchronous initial or terminal callback.</summary>
        /// <param name="source">The borrowed pipeline.</param>
        internal void Start(IObservable<TValue> source)
        {
            _starting = true;
            IDisposable? upstream = null;
            Exception? subscriptionError = null;
            try
            {
                upstream = source.Subscribe(this);
            }
            catch (Exception error)
            {
                subscriptionError = error;
            }

            _starting = false;
            FinishStarting(upstream, subscriptionError);
        }

        /// <summary>Combines synchronous terminal and returned-token failures without losing the original error.</summary>
        /// <param name="original">The first failure.</param>
        /// <param name="additional">The subsequent failure.</param>
        /// <returns>The preserved failure, or null.</returns>
        private static Exception? Combine(Exception? original, Exception? additional)
        {
            if (original is null)
            {
                return additional;
            }

            return additional is null || ReferenceEquals(original, additional)
                ? original
                : new AggregateException("Validation adapter and subscription handoff both failed.", original, additional);
        }

        /// <summary>Delivers a completed synchronous terminal handoff or propagates a failed subscription.</summary>
        /// <param name="receiver">The borrowed receiver captured before cleanup.</param>
        /// <param name="terminal">Whether the source supplied a terminal notification.</param>
        /// <param name="failure">The combined producer and cleanup failure, if any.</param>
        private static void DeliverTerminal(IObserver<TValue>? receiver, bool terminal, Exception? failure)
        {
            if (terminal)
            {
                if (failure is not null)
                {
                    receiver?.OnError(failure);
                }
                else
                {
                    receiver?.OnCompleted();
                }
            }
            else if (failure is not null)
            {
                ExceptionDispatchInfo.Throw(failure);
            }
        }

        /// <summary>Finishes synchronous handoff before delivering a terminal notification or callback failure.</summary>
        /// <param name="upstream">The returned owned subscription.</param>
        /// <param name="subscriptionError">Any failure before a subscription could be returned.</param>
        private void FinishStarting(IDisposable? upstream, Exception? subscriptionError)
        {
            if (!_disposed && subscriptionError is null)
            {
                _upstream = upstream;
                return;
            }

            var receiver = _observer;
            _observer = null;
            var initialFailure = _initialFailure;
            var terminalError = _pendingError;
            var completed = _pendingCompletion;
            _initialFailure = null;
            _pendingError = null;
            _pendingCompletion = false;
            _disposed = true;
            Exception? cleanupError = null;
            try
            {
                upstream?.Dispose();
            }
            catch (Exception error)
            {
                cleanupError = error;
            }

            if (initialFailure is not null)
            {
                ExceptionDispatchInfo.Throw(Combine(Combine(initialFailure, subscriptionError), cleanupError)!);
            }

            var failure = Combine(Combine(terminalError, subscriptionError), cleanupError);
            DeliverTerminal(receiver, terminalError is not null || completed, failure);
        }

        /// <summary>Preserves a callback or subscription failure before owned cleanup failures.</summary>
        /// <param name="error">The original failure.</param>
        /// <exception cref="AggregateException">Both operation and upstream cleanup fail.</exception>
        private void CleanupAfterFailure(Exception error)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Validation adapter and upstream cleanup both failed.", error, cleanupError);
            }
        }
    }
}
