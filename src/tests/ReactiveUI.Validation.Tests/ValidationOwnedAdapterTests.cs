// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
#else
using ReactiveUI.Validation.Capabilities;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Checks synchronous handoff and release of borrowed captures by retained adapter tokens.</summary>
[SuppressMessage("Usage", "SST2496:Repeated disposal", Justification = "These lifetime regressions deliberately end subscriptions from cleanup callbacks.")]
public class ValidationOwnedAdapterTests
{
    /// <summary>The original producer failure message.</summary>
    private const string ProducerFailure = "producer failure";

    /// <summary>The returned-token cleanup failure message.</summary>
    private const string CleanupFailure = "returned cleanup failure";

    /// <summary>The initial empty aggregate and first row publication count.</summary>
    private const int InitialPublications = 2;

    /// <summary>Checks initial errors are delivered once after the returned subscription has been cleaned up.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task OwnedTerminationWaitsForReturnedTokenAndPreservesOriginalError()
    {
        var original = new InvalidOperationException(ProducerFailure);
        var cleanup = new InvalidOperationException(CleanupFailure);
        var cleaned = false;
        var failures = new List<Exception>();
        var source = new TestSource<IValidationState>(observer => observer.OnError(original), () =>
        {
            cleaned = true;
            throw cleanup;
        });
        using var token = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            new ValidationOwnedObservable<IValidationState>(source),
            static _ => { },
            error =>
            {
                if (!cleaned)
                {
                    throw new InvalidOperationException("Error delivery preceded subscription cleanup.");
                }

                failures.Add(error);
            });
        await Assert.That(failures.Count).IsEqualTo(1);
        await Assert.That(failures[0]).IsTypeOf<AggregateException>();
        var failure = (AggregateException)failures[0];
        await Assert.That(failure.InnerExceptions[0]).IsSameReferenceAs(original);
        await Assert.That(failure.InnerExceptions[1]).IsSameReferenceAs(cleanup);
        source.Emit(ValidationState.Valid);
        await Assert.That(failures.Count).IsEqualTo(1);
    }

    /// <summary>Checks an initial receiver failure remains a synchronous failure and includes returned-token cleanup.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task OwnedCallbackFailureDoesNotInvokeOnErrorAndKeepsCleanupFailure()
    {
        var original = new InvalidOperationException(ProducerFailure);
        var cleanup = new InvalidOperationException(CleanupFailure);
        var errors = 0;
        var source = new TestSource<IValidationState>(
            static observer =>
            {
                try
                {
                    observer.OnNext(ValidationState.Valid);
                }
                catch (InvalidOperationException)
                {
                    // This source returns its token even when the initial receiver throws.
                }
            },
            () => throw cleanup);
        Exception? delivered = null;
        try
        {
            _ = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(new ValidationOwnedObservable<IValidationState>(source), _ => throw original, _ => errors++);
        }
        catch (Exception error)
        {
            delivered = error;
        }

        await Assert.That(delivered).IsTypeOf<AggregateException>();
        var failure = (AggregateException)delivered!;
        await Assert.That(failure.InnerExceptions[0]).IsSameReferenceAs(original);
        await Assert.That(failure.InnerExceptions[1]).IsSameReferenceAs(cleanup);
        await Assert.That(errors).IsEqualTo(0);
    }

    /// <summary>Checks initial snapshot and live row failures preserve returned-token cleanup on the public collection route.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionSynchronousFailureIncludesReturnedTokenCleanup()
    {
        var original = new InvalidOperationException(ProducerFailure);
        var cleanup = new InvalidOperationException(CleanupFailure);
        var failures = new List<Exception>();
        var snapshots = new TestSource<IReadOnlyCollection<CollectionRow>>(observer => observer.OnError(original), () => throw cleanup);
        using var token = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            Observe(snapshots, new()),
            static _ => { },
            failures.Add);
        await Assert.That(failures.Count).IsEqualTo(1);
        await Assert.That(failures[0]).IsTypeOf<AggregateException>();
        var failure = (AggregateException)failures[0];
        await Assert.That(failure.InnerExceptions[0]).IsSameReferenceAs(original);
        await Assert.That(failure.Flatten().InnerExceptions).Contains(cleanup);

        failures.Clear();
        var liveSnapshots = new TestSource<IReadOnlyCollection<CollectionRow>>();
        var rowStates = new TestSource<IValidationState>(observer => observer.OnError(original), () => throw cleanup);
        using var liveToken = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(Observe(liveSnapshots, new()), static _ => { }, failures.Add);
        liveSnapshots.Emit([new CollectionRow(new object(), rowStates)]);
        await Assert.That(failures.Count).IsEqualTo(1);
        await Assert.That(failures[0]).IsTypeOf<AggregateException>();
        var rowFailure = (AggregateException)failures[0];
        await Assert.That(rowFailure.InnerExceptions[0]).IsSameReferenceAs(original);
        await Assert.That(rowFailure.Flatten().InnerExceptions).Contains(cleanup);
    }

    /// <summary>Checks a retained disposed token does not retain its borrowed receiver even if the source retains stale callbacks.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DisposedOwnedTokenReleasesBorrowedObserver()
    {
        var (token, source, capture) = CreateDisposedOwnedToken();
        await Assert.That(IsCollected(capture)).IsTrue();
        source.Emit(ValidationState.Valid);
        GC.KeepAlive(token);
        GC.KeepAlive(source);
    }

    /// <summary>Checks a disposed collection releases selectors, factories, pending policies and its borrowed receiver.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DisposedCollectionTokenReleasesBorrowedPolicies()
    {
        var (token, source, capture) = CreateDisposedCollectionToken();
        await Assert.That(IsCollected(capture)).IsTrue();
        source.Emit([]);
        GC.KeepAlive(token);
        GC.KeepAlive(source);
    }

    /// <summary>Checks row-removal cleanup can dispose the aggregate without admitting or retaining the next snapshot.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionRemovalCleanupDisposalDoesNotAdmitOrRetainNextSnapshot()
    {
        var (token, source, key, created, published) = CreateReentrantlyDisposedCollection();
        await Assert.That(created).IsEqualTo(1);
        await Assert.That(published).IsEqualTo(InitialPublications);
        await Assert.That(IsCollected(key)).IsTrue();
        source.Emit([]);
        GC.KeepAlive(token);
        GC.KeepAlive(source);
    }

    /// <summary>Creates an ended receiver lifetime outside the frame that forces collection.</summary>
    /// <returns>The retained token, stale-callback source and weak borrowed receiver.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IDisposable Token, TestSource<IValidationState> Source, WeakReference<Capture> Capture) CreateDisposedOwnedToken()
    {
        var capture = new Capture();
        var source = new TestSource<IValidationState>();
        var token = new ValidationOwnedObservable<IValidationState>(source).Subscribe(capture);
        token.Dispose();
        return (token, source, new(capture));
    }

    /// <summary>Creates ended collection policies outside the frame that forces collection.</summary>
    /// <returns>The retained token, stale-callback source and weak borrowed policies.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IDisposable Token, TestSource<IReadOnlyCollection<CollectionRow>> Source, WeakReference<Capture> Capture) CreateDisposedCollectionToken()
    {
        var capture = new Capture();
        var source = new TestSource<IReadOnlyCollection<CollectionRow>>();
        var token = Observe(source, capture).Subscribe(capture);
        source.Emit([new CollectionRow(new object())]);
        token.Dispose();
        return (token, source, new(capture));
    }

    /// <summary>Creates a next snapshot whose admission is interrupted by removed-row cleanup.</summary>
    /// <returns>The retained lifetimes, weak next key and observed factory/publication counts.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IDisposable Token, TestSource<IReadOnlyCollection<CollectionRow>> Source, WeakReference<object> Key, int Created, int Published) CreateReentrantlyDisposedCollection()
    {
        var source = new TestSource<IReadOnlyCollection<CollectionRow>>();
        var first = new CollectionRow(new object());
        var nextKey = new object();
        var next = new CollectionRow(nextKey);
        IDisposable? token = null;
        var created = 0;
        var published = 0;
        var stream = ValidationCollection.Observe(
            ReactiveUI.Primitives.Signals.Signal.Return<IObservable<IReadOnlyCollection<CollectionRow>>?>(source),
            static row => row.Key,
            row =>
            {
                created++;
                return new ValidationRowLease(row.States, new Cleanup(() => token!.Dispose()));
            },
            static _ => ValidationState.Valid,
            ValidationState.Valid);
        token = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(stream, _ => published++);
        source.Emit([first]);
        source.Emit([next]);
        return (token, source, new(nextKey), created, published);
    }

    /// <summary>Forces collection only after all strong temporary references have left their creation frame.</summary>
    /// <typeparam name="TValue">The borrowed reference.</typeparam>
    /// <param name="reference">The weak reference.</param>
    /// <returns>Whether the borrowed object is no longer retained.</returns>
    [SuppressMessage("Performance", "PSH1021:Avoid explicit collection", Justification = "This retained-token lifetime regression must force collection after its no-inline creation frame ends.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsCollected<TValue>(WeakReference<TValue> reference)
        where TValue : class
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return !reference.TryGetTarget(out _);
    }

    /// <summary>Creates the public collection route with every policy and receiver sharing one borrowed object.</summary>
    /// <param name="source">The borrowed snapshot source.</param>
    /// <param name="capture">The borrowed policies.</param>
    /// <returns>The cold aggregate stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IValidationState> Observe(IObservable<IReadOnlyCollection<CollectionRow>> source, Capture capture) =>
        ValidationCollection.Observe(
            ReactiveUI.Primitives.Signals.Signal.Return<IObservable<IReadOnlyCollection<CollectionRow>>?>(source),
            capture.GetKey,
            capture.CreateLease,
            capture.Project,
            capture);

    /// <summary>A borrowed row carrying a distinct key and nonterminating state source.</summary>
    /// <param name="key">The borrowed key.</param>
    /// <param name="states">The optional borrowed state producer.</param>
    private sealed class CollectionRow(object key, TestSource<IValidationState>? states = null)
    {
        /// <summary>Gets the borrowed key.</summary>
        internal object Key { get; } = key;

        /// <summary>Gets the borrowed state source.</summary>
        internal TestSource<IValidationState> States { get; } = states ?? new();
    }

    /// <summary>A borrowed policy and observer whose lifetime can be observed independently.</summary>
    private sealed class Capture : IValidationState, IObserver<IValidationState>
    {
        /// <inheritdoc/>
        public IValidationText Text => ValidationState.Valid.Text;

        /// <inheritdoc/>
        public bool IsValid => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(IValidationState value) => GC.KeepAlive(value);

        /// <inheritdoc/>
        public void OnError(Exception error) => throw error;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCompleted() => GC.KeepAlive(this);

        /// <summary>Selects a borrowed row key through an instance delegate.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal object GetKey(CollectionRow row)
        {
            GC.KeepAlive(this);
            return row.Key;
        }

        /// <summary>Creates a row lease through an instance delegate.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The owned subscription-only lease.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValidationRowLease CreateLease(CollectionRow row)
        {
            GC.KeepAlive(this);
            return new(row.States);
        }

        /// <summary>Projects an aggregate through an instance delegate.</summary>
        /// <param name="_">The current states, deliberately ignored by this lifetime policy.</param>
        /// <returns>This borrowed state policy.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Capture Project(IReadOnlyList<IValidationState> _) => this;
    }

    /// <summary>A source intentionally retaining stale callbacks after its returned token is disposed.</summary>
    /// <typeparam name="TValue">The notification value.</typeparam>
    /// <param name="start">The optional synchronous subscription callback.</param>
    /// <param name="cleanup">The optional returned-token cleanup callback.</param>
    private sealed class TestSource<TValue>(Action<IObserver<TValue>>? start = null, Action? cleanup = null) : IObservable<TValue>
    {
        /// <summary>The retained callback used to prove adapters release their own captures.</summary>
        private IObserver<TValue>? _observer;

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            _observer = observer;
            start?.Invoke(observer);
            return new Cleanup(cleanup ?? (static () => { }));
        }

        /// <summary>Delivers a value even after cleanup to check stale notification rejection.</summary>
        /// <param name="value">The borrowed notification.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Emit(TValue value) => _observer?.OnNext(value);
    }

    /// <summary>An explicit transferred cleanup action.</summary>
    /// <param name="dispose">The cleanup action.</param>
    private sealed class Cleanup(Action dispose) : IDisposable
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => dispose();
    }
}
