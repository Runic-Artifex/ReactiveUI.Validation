// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
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

/// <summary>Exercises owned async, collection, scheduling, output-factory and stack-only routes.</summary>
[SuppressMessage("Concurrency", "PSH1302:Use asynchronous task continuations", Justification = "Inline test completions make explicit owner scheduling assertions deterministic; no callback blocks.")]
[SuppressMessage("Usage", "SST2496:Repeated disposal", Justification = "These ownership regressions deliberately exercise early, repeated and reentrant disposal.")]
public class ValidationCapabilityAdapterTests
{
    /// <summary>The second stable row/input key.</summary>
    private const int SecondKey = 2;

    /// <summary>The number of items or replacements in a pair.</summary>
    private const int TwoItems = 2;

    /// <summary>The length of the stack-only example.</summary>
    private const int ExpectedSpanLength = 5;

    /// <summary>The FirstInput test value.</summary>
    private const string FirstInput = "first";

    /// <summary>The ThirdInput test value.</summary>
    private const string ThirdInput = "third";

    /// <summary>The PendingMessage test value.</summary>
    private const string PendingMessage = "checking";

    /// <summary>The ObsoleteMessage test value.</summary>
    private const string ObsoleteMessage = "obsolete";

    /// <summary>The FailureMessage test value.</summary>
    private const string FailureMessage = "offline";

    /// <summary>The RequiredMessage test value.</summary>
    private const string RequiredMessage = "required";

    /// <summary>The expected serialized reentry trace.</summary>
    private static readonly string[] ReentryTrace = ["begin1", "end1", "begin2", "end2"];

    /// <summary>Checks cancellation, late success rejection, explicit completion dispatch and caller pending validity.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task LatestAsyncCancelsSupersededAndDisposedRequestsAndDispatchesCompletion()
    {
        using var inputs = new BehaviorSubject<string>(FirstInput);
        var scheduler = new ValidationManualScheduler();
        var requests = new Dictionary<string, TaskCompletionSource<IValidationState>>();
        var tokens = new Dictionary<string, CancellationToken>();
        var states = new List<IValidationState>();
        var pending = new ValidationState(false, PendingMessage);
        var stream = ValidationAsync.ForLatest(
            inputs,
            (value, token) =>
        {
            tokens.Add(value, token);
            var request = new TaskCompletionSource<IValidationState>();
            requests.Add(value, request);
            return request.Task;
        },
            pending,
            scheduler);
        var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(stream, states.Add);
        await Assert.That(states[^1]).IsSameReferenceAs(pending);
        inputs.OnNext("second");
        await Assert.That(tokens[FirstInput].IsCancellationRequested).IsTrue();
        requests[FirstInput].SetResult(new ValidationState(false, ObsoleteMessage));
        scheduler.Drain();
        await Assert.That(states.TrueForAll(static state => state.Text!.ToSingleLine() != ObsoleteMessage)).IsTrue();
        requests["second"].SetResult(ValidationState.Valid);
        await Assert.That(states[^1]).IsSameReferenceAs(pending);
        scheduler.Drain();
        await Assert.That(states[^1]).IsSameReferenceAs(ValidationState.Valid);
        inputs.OnNext(ThirdInput);
        subscription.Dispose();
        subscription.Dispose();
        await Assert.That(tokens[ThirdInput].IsCancellationRequested).IsTrue();
        await Assert.That(inputs.HasObservers).IsFalse();
        var count = states.Count;
        requests[ThirdInput].SetResult(ValidationState.Valid);
        scheduler.Drain();
        await Assert.That(states.Count).IsEqualTo(count);
    }

    /// <summary>Checks explicit failure-state mapping and same-turn blocking/advisory admission before completion drains.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task AsyncPendingAndFailurePoliciesPreserveIndependentContexts()
    {
        using var inputs = new BehaviorSubject<int>(0);
        using var blocking = new ValidationContext();
        using var advisory = new ValidationContext();
        var scheduler = new ValidationManualScheduler();
        var failure = new InvalidOperationException(FailureMessage);
        var stream = ValidationAsync.ForLatest(
            inputs,
            (_, _) => Task.FromException<IValidationState>(failure),
            new ValidationState(false, PendingMessage),
            scheduler,
            static error => new ValidationState(false, error.Message));
        using var blockingRule = blocking.AddObservableRule(stream, ["Name"]);
        using var advisoryRule = advisory.AddObservableRule(ValidationAsync.ForLatest(inputs, static (_, _) => Task.FromResult(ValidationState.Valid), ValidationState.Valid, scheduler), ["Name"]);
        await Assert.That(blocking.GetIsValid()).IsFalse();
        await Assert.That(advisory.GetIsValid()).IsTrue();
        scheduler.Drain();
        await Assert.That(blockingRule.Message).Contains(FailureMessage);
        await Assert.That(advisory.GetIsValid()).IsTrue();
    }

    /// <summary>Checks current failures terminate on the selected owner and release source subscriptions.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task AsyncUnmappedFailureTerminatesAndCompletionWaitsForLatest()
    {
        using var inputs = new Subject<int>();
        var scheduler = new ValidationManualScheduler();
        var failure = new InvalidOperationException("failed");
        Exception? error = null;
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            ValidationAsync.ForLatest(inputs, (_, _) => Task.FromException<IValidationState>(failure), ValidationState.Valid, scheduler),
            static _ => { },
            value => error = value);
        inputs.OnNext(0);
        await Assert.That(error).IsNull();
        scheduler.Drain();
        await Assert.That(error).IsSameReferenceAs(failure);
        await Assert.That(inputs.HasObservers).IsFalse();

        using var completingInputs = new Subject<int>();
        var request = new TaskCompletionSource<IValidationState>();
        var complete = false;
        using var completing = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            ValidationAsync.ForLatest(completingInputs, (_, _) => request.Task, ValidationState.Valid, scheduler),
            static _ => { },
            static error => throw error,
            () => complete = true);
        completingInputs.OnNext(0);
        completingInputs.OnCompleted();
        await Assert.That(complete).IsFalse();
        request.SetResult(ValidationState.Valid);
        scheduler.Drain();
        await Assert.That(complete).IsTrue();
    }

    /// <summary>Checks asynchronous failure policy executes only for the current request on the explicit owner.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task AsyncFailurePolicyRunsOnOwnerAndRejectsObsoleteFaults()
    {
        using var inputs = new BehaviorSubject<int>(0);
        var scheduler = new ValidationManualScheduler();
        var requests = new[] { new TaskCompletionSource<IValidationState>(), new TaskCompletionSource<IValidationState>() };
        var onOwner = false;
        var policyCalls = 0;
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            ValidationAsync.ForLatest(inputs, (value, _) => requests[value].Task, ValidationState.Valid, scheduler, _ =>
        {
            if (!onOwner)
            {
                throw new InvalidOperationException("Failure policy escaped owner scheduling.");
            }

            policyCalls++;
            return new ValidationState(false, FailureMessage);
        }),
            static _ => { });
        inputs.OnNext(1);
        requests[0].SetException(new InvalidOperationException(ObsoleteMessage));
        scheduler.Drain();
        await Assert.That(policyCalls).IsEqualTo(0);
        requests[1].SetException(new InvalidOperationException("current"));
        await Assert.That(policyCalls).IsEqualTo(0);
        onOwner = true;
        scheduler.Drain();
        await Assert.That(policyCalls).IsEqualTo(1);
    }

    /// <summary>Checks stable row references survive edits, additions, reordering and unchanged snapshots.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionRetainsUnchangedRowsAndCleansRemovedReplacedAndOuterSources()
    {
        using var first = new Row(1, false);
        using var second = new Row(SecondKey, true);
        using var replacement = new Row(1, true);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([]);
        using var otherSnapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([second]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var states = new List<IValidationState>();
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(Collection(sources), states.Add);
        await Assert.That(states[^1].IsValid).IsFalse();
        snapshots.OnNext([first]);
        await Assert.That(first.Created).IsEqualTo(1);
        await Assert.That(states[^1].IsValid).IsFalse();
        first.States.OnNext(ValidationState.Valid);
        await Assert.That(states[^1].IsValid).IsTrue();
        snapshots.OnNext([second, first]);
        snapshots.OnNext([first, second]);
        await Assert.That(first.Created).IsEqualTo(1);
        await Assert.That(second.Created).IsEqualTo(1);
        snapshots.OnNext([replacement, second]);
        await Assert.That(first.Cleaned).IsEqualTo(1);
        await Assert.That(first.States.HasObservers).IsFalse();
        var count = states.Count;
        first.States.OnNext(new ValidationState(false, "stale"));
        await Assert.That(states.Count).IsEqualTo(count);
        snapshots.OnNext([]);
        await Assert.That(replacement.Cleaned).IsEqualTo(1);
        await Assert.That(second.Cleaned).IsEqualTo(1);
        sources.OnNext(otherSnapshots);
        await Assert.That(second.Created).IsEqualTo(TwoItems);
        await Assert.That(snapshots.HasObservers).IsFalse();
        await Assert.That(states[^1].IsValid).IsTrue();
        sources.OnNext(null);
        await Assert.That(second.Cleaned).IsEqualTo(TwoItems);
        await Assert.That(otherSnapshots.HasObservers).IsFalse();
        await Assert.That(states[^1].IsValid).IsFalse();
    }

    /// <summary>Checks disposal from an initial row callback rejects the late subscription handoff and owns only the helper.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionReentrantMembershipAndTransferredHelpersCleanUp()
    {
        using var row = new Row(1, false);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([row]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        using var context = new ValidationContext();
        var count = 0;
        var stream = ValidationCollection.Observe(
            sources,
            static value => value.Key,
            value =>
        {
            var helper = context.AddObservableRule(value.States, ["Name"]);
            return new ValidationRowLease(helper.ValidationChanged, helper);
        },
            static states => new ValidationState(states.Count > 0 && states.All(static state => state.IsValid), "rows"),
            ValidationState.Valid);
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(stream, _ =>
        {
            count++;
            if (count == 1)
            {
                snapshots.OnNext([]);
            }
        });
        await Assert.That(context.Validations.Items).IsEmpty();
        await Assert.That(row.States.HasObservers).IsFalse();
        await Assert.That(count).IsEqualTo(TwoItems);
        subscription.Dispose();
        await Assert.That(snapshots.HasObservers).IsFalse();
        await Assert.That(sources.HasObservers).IsFalse();
        await Assert.That(context.Validations.Items).IsEmpty();
    }

    /// <summary>Checks duplicate keys and row failures terminate with complete owned cleanup.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionRejectsDuplicateKeysAndTerminatesFailedRows()
    {
        using var first = new Row(1, true);
        using var duplicate = new Row(1, true);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([first]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(Collection(sources), static _ => { });
        await Assert.That(() => snapshots.OnNext([first, duplicate])).Throws<ArgumentException>();
        await Assert.That(first.Cleaned).IsEqualTo(1);
        await Assert.That(first.States.HasObservers).IsFalse();
        await Assert.That(snapshots.HasObservers).IsFalse();

        using var failingRow = new Row(SecondKey, true);
        using var failingSnapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([failingRow]);
        using var failingSources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(failingSnapshots);
        var failure = new InvalidOperationException("row failed");
        Exception? error = null;
        using var failing = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(Collection(failingSources), static _ => { }, value => error = value);
        failingRow.States.OnError(failure);
        await Assert.That(error).IsSameReferenceAs(failure);
        await Assert.That(failingRow.Cleaned).IsEqualTo(1);
        await Assert.That(failingSnapshots.HasObservers).IsFalse();
    }

    /// <summary>Checks finite null selection completes and row completion releases only its owned lifetime.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionNullSelectionAndCompletedRowsReleaseOwnedLifetimes()
    {
        var complete = false;
        using var empty = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            Collection(ReactiveUI.Primitives.Signals.Signal.Return<IObservable<IReadOnlyCollection<Row>>?>(null)),
            static _ => { },
            static error => throw error,
            () => complete = true);
        await Assert.That(complete).IsTrue();
        using var row = new Row(1, true);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([row]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var states = new List<IValidationState>();
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(Collection(sources), states.Add);
        var count = states.Count;
        row.States.OnCompleted();
        await Assert.That(row.Cleaned).IsEqualTo(1);
        await Assert.That(row.States.HasObservers).IsFalse();
        await Assert.That(states.Count).IsEqualTo(count);
        await Assert.That(states[^1].IsValid).IsTrue();
        snapshots.OnNext([]);
        await Assert.That(row.Cleaned).IsEqualTo(1);
    }

    /// <summary>Checks removing an asynchronous row cancels its request and rejects a late result.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionRemovalCancelsPendingAsyncRowAndRejectsLateResult()
    {
        using var row = new Row(1, false);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([row]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var scheduler = new ValidationManualScheduler();
        var request = new TaskCompletionSource<IValidationState>();
        CancellationToken cancellation = default;
        var states = new List<IValidationState>();
        var stream = ValidationCollection.Observe(
            sources,
            static value => value.Key,
            value => new ValidationRowLease(ValidationAsync.ForLatest(
                value.States,
                (_, token) =>
                {
                    cancellation = token;
                    return request.Task;
                },
                new ValidationState(false, PendingMessage),
                scheduler)),
            static values => new ValidationState(values.Count > 0 && values.All(static state => state.IsValid), "rows"),
            ValidationState.Valid);
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(stream, states.Add);
        snapshots.OnNext([]);
        await Assert.That(cancellation.IsCancellationRequested).IsTrue();
        var count = states.Count;
        request.SetResult(ValidationState.Valid);
        scheduler.Drain();
        await Assert.That(states.Count).IsEqualTo(count);
        await Assert.That(row.States.HasObservers).IsFalse();
    }

    /// <summary>Checks producer and cleanup errors remain identifiable while every outer and row subscription ends.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionProducerAndCleanupFailuresRetainOriginalAndReleaseOuter()
    {
        using var row = new Row(1, true);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([row]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var cleanupFailure = new InvalidOperationException("owned cleanup");
        Exception? delivered = null;
        var stream = ValidationCollection.Observe(
            sources,
            static value => value.Key,
            value => new ValidationRowLease(value.States, new Cleanup(() => throw cleanupFailure)),
            static values => values[0],
            ValidationState.Valid);
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(stream, static _ => { }, error => delivered = error);
        snapshots.OnNext([row, row]);
        await Assert.That(delivered).IsTypeOf<AggregateException>();
        var aggregate = (AggregateException)delivered!;
        await Assert.That(aggregate.InnerExceptions[0]).IsTypeOf<ArgumentException>();
        await Assert.That(aggregate.Flatten().InnerExceptions).Contains(cleanupFailure);
        await Assert.That(row.States.HasObservers).IsFalse();
        await Assert.That(snapshots.HasObservers).IsFalse();
        await Assert.That(sources.HasObservers).IsFalse();
    }

    /// <summary>Checks an observer failure propagates synchronously after cleanup without invoking that observer's error callback.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CollectionObserverFailurePropagatesAfterOwnedCleanup()
    {
        using var row = new Row(1, true);
        using var snapshots = new BehaviorSubject<IReadOnlyCollection<Row>>([]);
        using var sources = new BehaviorSubject<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var failure = new InvalidOperationException("observer failure");
        var shouldThrow = false;
        var errors = 0;
        using var subscription = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(
            Collection(sources),
            _ =>
            {
                if (shouldThrow)
                {
                    throw failure;
                }
            },
            _ => errors++);
        shouldThrow = true;
        await Assert.That(() => snapshots.OnNext([row])).Throws<InvalidOperationException>();
        await Assert.That(errors).IsEqualTo(0);
        await Assert.That(row.Cleaned).IsEqualTo(1);
        await Assert.That(row.States.HasObservers).IsFalse();
        await Assert.That(snapshots.HasObservers).IsFalse();
        await Assert.That(sources.HasObservers).IsFalse();
    }

    /// <summary>Checks serialized reentry and presentation deferral leave synchronous domain admission unchanged.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ExplicitSchedulingQueuesReentryAndDiscardsDisposedPresentation()
    {
        using var inputs = new Subject<int>();
        var scheduler = new ValidationManualScheduler();
        var trace = new List<string>();
        using var serialized = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(ValidationScheduling.SerializeInputs(inputs, scheduler), value =>
        {
            trace.Add($"begin{value}");
            if (value == 1)
            {
                inputs.OnNext(SecondKey);
            }

            trace.Add($"end{value}");
        });
        inputs.OnNext(1);
        await Assert.That(trace).IsEmpty();
        scheduler.Drain();
        await Assert.That(trace).IsEquivalentTo(ReentryTrace);
        using var context = new ValidationContext();
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var rule = context.AddObservableRule(states, ["Name"]);
        var shown = new List<bool>();
        using var presentation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(ValidationScheduling.PresentOn(context.Valid, scheduler), shown.Add);
        scheduler.Drain();
        states.OnNext(new ValidationState(false, RequiredMessage));
        await Assert.That(context.GetIsValid()).IsFalse();
        await Assert.That(shown[^1]).IsTrue();
        presentation.Dispose();
        scheduler.Drain();
        await Assert.That(shown[^1]).IsTrue();
    }

    /// <summary>Checks only the output type needs explicit spelling, including nullable reference and value outputs.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task OutputOnlyFactoriesInferSourcesAndPreserveNullableOutputs()
    {
        using var context = new ValidationContext();
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var helper = context.AddObservableRule(states, ["Name"]);
        using var sources = new BehaviorSubject<ValidationHelper?>(helper);
        var text = new List<string?>();
        var number = new List<int?>();
        using var binding = ValidationOutput<string?>.FromStates(sources, static value => value.ValidationChanged, static state => state.IsValid ? null : state.Text!.ToSingleLine(), text.Add);
        using var contexts = new BehaviorSubject<IValidationContext?>(context);
        using var property = ValidationOutput<int?>.FromPropertyStates(contexts, static value => value, "Name", static values => values.All(static state => state.IsValid) ? null : 1, number.Add);
        await Assert.That(text[^1]).IsNull();
        await Assert.That(number[^1]).IsNull();
        states.OnNext(new ValidationState(false, RequiredMessage));
        await Assert.That(text[^1]).IsEqualTo(RequiredMessage);
        await Assert.That(number[^1]).IsEqualTo(1);
    }

    /// <summary>Checks stack-only input is read synchronously and copied structs remain distinct storage.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SnapshotAcceptsRefStructAndReadsOnlyTheSuppliedStorage()
    {
        var (length, state) = ReadSpan("runic");
        await Assert.That(length).IsEqualTo(ExpectedSpanLength);
        await Assert.That(state.IsValid).IsTrue();
        var original = new StackValue(1);
        var copy = original;
        original = new(SecondKey);
        var copiedValue = ValidationSnapshot.Read(in copy, static (in StackValue value) => value.Value);
        var originalValue = ValidationSnapshot.Read(in original, static (in StackValue value) => value.Value);
        await Assert.That(copiedValue).IsEqualTo(1);
        await Assert.That(originalValue).IsEqualTo(TwoItems);
    }

    /// <summary>Builds the reusable collection route with an explicit nonempty aggregate policy.</summary>
    /// <param name="sources">The borrowed outer selection.</param>
    /// <returns>The owned aggregate stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IValidationState> Collection(IObservable<IObservable<IReadOnlyCollection<Row>>?> sources) =>
        ValidationCollection.Observe(
            sources,
            static row => row.Key,
            static row => row.CreateLease(),
            static states => new ValidationState(states.Count > 0 && states.All(static state => state.IsValid), "rows"),
            new ValidationState(false, PendingMessage));

    /// <summary>Restricts the stack-only borrow to synchronous execution before asynchronous assertions.</summary>
    /// <param name="text">The span owner.</param>
    /// <returns>The ordinary outputs.</returns>
    private static (int Length, IValidationState State) ReadSpan(string text)
    {
        var span = text.AsSpan();
        var length = ValidationSnapshot.Read(in span, static (in ReadOnlySpan<char> value) => value.Length);
        var state = ValidationSnapshot.Validate(in span, static (in ReadOnlySpan<char> value) => new ValidationState(!value.IsEmpty, RequiredMessage));
        return (length, state);
    }

    /// <summary>A mutable value used to distinguish actual and copied snapshot storage.</summary>
    /// <param name="value">The immutable snapshot value.</param>
    private readonly struct StackValue(int value)
    {
        /// <summary>Gets or sets the stored value.</summary>
        internal int Value { get; } = value;
    }

    /// <summary>A borrowed row with measurable explicitly owned lease lifetimes.</summary>
    /// <param name="key">The stable row key.</param>
    /// <param name="valid">The initial validity.</param>
    private sealed class Row(int key, bool valid) : IDisposable
    {
        /// <summary>Gets the stable key.</summary>
        internal int Key { get; } = key;

        /// <summary>Gets the borrowed state source.</summary>
        internal BehaviorSubject<IValidationState> States { get; } = new(new ValidationState(valid, "row"));

        /// <summary>Gets the number of created row leases.</summary>
        internal int Created { get; private set; }

        /// <summary>Gets the number of cleaned owned lifetimes.</summary>
        internal int Cleaned { get; private set; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => States.Dispose();

        /// <summary>Creates a transferred lifetime without transferring the borrowed row or source.</summary>
        /// <returns>The owned row lease.</returns>
        internal ValidationRowLease CreateLease()
        {
            Created++;
            return new(States, new Cleanup(() => Cleaned++));
        }
    }

    /// <summary>A measurable transferred lifetime.</summary>
    /// <param name="dispose">The owned cleanup action.</param>
    private sealed class Cleanup(Action dispose) : IDisposable
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => dispose();
    }
}
