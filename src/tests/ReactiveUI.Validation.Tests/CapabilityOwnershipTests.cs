// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Disposable = ReactiveUI.Primitives.Disposables.Scope;

#if REACTIVE_SHIM
using System.Reactive.Linq;
#else
using Observable = ReactiveUI.Primitives.Signals.Signal;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Verifies detachment independently of missing-owner presentation policies.</summary>
public class CapabilityOwnershipTests
{
    /// <summary>The first selected producer's error text.</summary>
    private const string FirstError = "first";

    /// <summary>The replacement producer's error text.</summary>
    private const string ReplacementError = "replacement";

    /// <summary>The late producer failure text.</summary>
    private const string LateError = "late producer failure";

    /// <summary>The capacity for a single model descriptor and attachment.</summary>
    private const int RegistryCapacity = 2;

    /// <summary>The count after one replacement source has been selected.</summary>
    private const int ReplacementCount = 2;

    /// <summary>The count after a selected source has been recovered twice.</summary>
    private const int RecoveryCount = 3;

    /// <summary>A suppressed missing state owner detaches even when its producer ignores disposal.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SuppressedStateOwnerIgnoresLateEventsAndRecovers()
    {
        var first = new NonCooperativeStates(new ValidationState(false, FirstError));
        var replacement = new NonCooperativeStates(new ValidationState(false, ReplacementError));
        var cell = new ValidationCell<IObservable<IValidationState>?>(first);
        var selector = new ValidationSelector<ValidationCell<IObservable<IValidationState>?>, IObservable<IValidationState>?>(source => new(
            () => source.Value is null
                ? ValidationRead<IObservable<IValidationState>?>.Missing([])
                : ValidationRead<IObservable<IValidationState>?>.Present(source.Value, []),
            [ValidationDependency.PropertyChanged(() => source, nameof(source.Value))],
            Suppress<IObservable<IValidationState>?>()));
        var observed = new List<IValidationState>();
        var failures = new List<Exception>();
        using var subscription = ValidationRuntime.ObserveState(cell, selector, ValidationInitialSequence.Actual)
            .Subscribe(observed.Add, failures.Add);
        await Assert.That(observed.Count).IsEqualTo(1);
        cell.Value = null;
        await Assert.That(first.Disposals).IsEqualTo(1);
        first.Emit(ValidationState.Valid);
        first.Fail(new InvalidOperationException(LateError));
        await Assert.That(observed.Count).IsEqualTo(1);
        await Assert.That(failures.Count).IsEqualTo(0);
        cell.Value = replacement;
        await Assert.That(observed.Count).IsEqualTo(ReplacementCount);
        await Assert.That(observed[^1]).IsSameReferenceAs(replacement.Initial);
    }

    /// <summary>Both missing models and missing helpers detach their selected state stream.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SuppressedModelAndHelperOwnersDetachIndependently()
    {
        using var model = new TestViewModel();
        using var firstHelper = model.AddObservableRule(Observable.Return(ValidationState.Valid), []);
        using var replacementHelper = model.AddObservableRule(Observable.Return(ValidationState.Valid), []);
        var view = new TestView(model);
        var models = new ValidationCell<TestViewModel?>(model);
        var helpers = new ValidationCell<ValidationHelper?>(firstHelper);
        var first = new NonCooperativeStates(new ValidationState(false, FirstError));
        var replacement = new NonCooperativeStates(new ValidationState(false, ReplacementError));
        var modelSelector = new ValidationSelector<TestView, TestViewModel?>(_ => new(
            () => models.Value is null
                ? ValidationRead<TestViewModel?>.Missing([])
                : ValidationRead<TestViewModel?>.Present(models.Value, []),
            [ValidationDependency.PropertyChanged(() => models, nameof(models.Value))],
            Suppress<TestViewModel?>()));
        var helperSelector = new ValidationSelector<TestViewModel, ValidationHelper?>(_ => new(
            () => helpers.Value is null
                ? ValidationRead<ValidationHelper?>.Missing([])
                : ValidationRead<ValidationHelper?>.Present(helpers.Value, []),
            [ValidationDependency.PropertyChanged(() => helpers, nameof(helpers.Value))],
            Suppress<ValidationHelper?>()));
        using var registry = new ValidationPlanRegistry(RegistryCapacity);
        using var attachment = registry.Attach(view);
        using var descriptor = registry.RegisterSelector(ValidationPlanRole.Model, null, modelSelector);
        var observedModels = new List<TestViewModel?>();
        var observed = new List<IValidationState>();
        var failures = new List<Exception>();
        using var modelSubscription = ValidationRuntime.ObserveModels<TestView, TestViewModel>(view).Subscribe(observedModels.Add);
        using var subscription = ValidationRuntime.ObserveViewState(
            view,
            helperSelector,
            helper => ReferenceEquals(helper, firstHelper) ? first : replacement,
            ValidationInitialSequence.Actual).Subscribe(observed.Add, failures.Add);
        models.Value = null;
        await Assert.That(observedModels[^1]).IsNull();
        await Assert.That(first.Disposals).IsEqualTo(1);
        first.Emit(ValidationState.Valid);
        await Assert.That(observed.Count).IsEqualTo(1);
        models.Value = model;
        await Assert.That(observed.Count).IsEqualTo(ReplacementCount);
        helpers.Value = null;
        await Assert.That(first.Disposals).IsEqualTo(ReplacementCount);
        first.Emit(ValidationState.Valid);
        first.Fail(new InvalidOperationException(LateError));
        await Assert.That(observed.Count).IsEqualTo(ReplacementCount);
        await Assert.That(failures.Count).IsEqualTo(0);
        helpers.Value = replacementHelper;
        await Assert.That(observed.Count).IsEqualTo(RecoveryCount);
        await Assert.That(observed[^1]).IsSameReferenceAs(replacement.Initial);
    }

    /// <summary>Suppression detaches property rules while present null still clears the presentation.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SuppressedContextOwnerDetachesRulesAndDistinguishesPresentNull()
    {
        using var first = new TestViewModel();
        using var replacement = new TestViewModel();
        var firstState = new ValidationState(false, FirstError);
        var replacementState = new ValidationState(false, ReplacementError);
        using var firstStates = new BehaviorSubject<IValidationState>(firstState);
        using var replacementStates = new BehaviorSubject<IValidationState>(replacementState);
        using var firstRule = first.AddObservableRule(firstStates, [nameof(TestViewModel.Name)]);
        using var replacementRule = replacement.AddObservableRule(replacementStates, [nameof(TestViewModel.Name)]);
        var cell = new ValidationCell<(bool Present, IValidationContext? Context)>((true, first.ValidationContext));
        var contexts = new ValidationSelector<ValidationCell<(bool Present, IValidationContext? Context)>, IValidationContext?>(source => new(
            () => source.Value.Present
                ? ValidationRead<IValidationContext?>.Present(source.Value.Context, [])
                : ValidationRead<IValidationContext?>.Missing([]),
            [ValidationDependency.PropertyChanged(() => source, nameof(source.Value))],
            Suppress<IValidationContext?>()));
        var path = new ValidationSelector<ValidationCell<(bool Present, IValidationContext? Context)>, ValidationPath>(static _ => new(
            static () => ValidationRead<ValidationPath>.Present(ValidationPath.Legacy(nameof(TestViewModel.Name)), []),
            [],
            ValidationObservationOptions<ValidationPath>.Default));
        var observed = new List<IList<IValidationState>>();
        using var subscription = ValidationRuntime.ObserveProperty(cell, contexts, path, true, ValidationInitialSequence.Actual)
            .Subscribe(observed.Add);
        await Assert.That(observed[^1][0]).IsSameReferenceAs(firstState);
        var beforeMissing = observed.Count;
        cell.Value = (false, null);
        firstStates.OnNext(ValidationState.Valid);
        await Assert.That(observed.Count).IsEqualTo(beforeMissing);
        cell.Value = (true, null);
        await Assert.That(observed[^1].Count).IsEqualTo(0);
        cell.Value = (true, replacement.ValidationContext);
        await Assert.That(observed[^1][0]).IsSameReferenceAs(replacementState);
    }

    /// <summary>Declared value fallbacks and comparers remain intact without manufacturing an absent owner.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task OrdinaryValueFallbackIsPreservedWithoutSyntheticOwnership()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        var valueSelector = new ValidationSelector<TestViewModel, bool>(static _ => new(
            static () => ValidationRead<bool>.Missing([]),
            [],
            new(ValidationMissingOwnerPolicy.Fallback, static () => true, EqualityComparer<bool>.Default, false)));
        var observedValues = new List<bool>();
        using var valueSubscription = ValidationRuntime.ObserveViewState(
            view,
            valueSelector,
            value =>
            {
                observedValues.Add(value);
                return Observable.Return(ValidationState.Valid);
            },
            ValidationInitialSequence.Actual).Subscribe();
        await Assert.That(observedValues.Count).IsEqualTo(1);
        await Assert.That(observedValues[0]).IsTrue();
        using var replacementContext = new ValidationContext(ImmediateSequencer.Instance);
        var objects = new ValidationCell<object>(model.ValidationContext);
        var objectSelector = new ValidationSelector<TestViewModel, object>(_ => new(
            () => ValidationRead<object>.Present(objects.Value, []),
            [ValidationDependency.PropertyChanged(() => objects, nameof(objects.Value))],
            new(ValidationMissingOwnerPolicy.DefaultValue, null, new EqualObjectComparer(), false)));
        var selectedObjects = new List<object>();
        using var objectSubscription = ValidationRuntime.ObserveViewState(
            view,
            objectSelector,
            value =>
            {
                selectedObjects.Add(value);
                return Observable.Return(ValidationState.Valid);
            },
            ValidationInitialSequence.Actual).Subscribe();
        objects.Value = replacementContext;
        await Assert.That(selectedObjects.Count).IsEqualTo(1);
        var fallback = new NonCooperativeStates(new ValidationState(false, FirstError));
        var ownerSelector = new ValidationSelector<TestViewModel, IObservable<IValidationState>?>(_ => new(
            static () => ValidationRead<IObservable<IValidationState>?>.Missing([]),
            [],
            new(ValidationMissingOwnerPolicy.Fallback, () => fallback, EqualityComparer<IObservable<IValidationState>?>.Default, false)));
        var observedStates = new List<IValidationState>();
        using var ownerSubscription = ValidationRuntime.ObserveState(model, ownerSelector, ValidationInitialSequence.Actual)
            .Subscribe(observedStates.Add);
        await Assert.That(fallback.Subscriptions).IsEqualTo(0);
        await Assert.That(observedStates[^1]).IsSameReferenceAs(ValidationState.Valid);
    }

    /// <summary>Creates the suppression policy for an explicit owner descriptor.</summary>
    /// <typeparam name="TValue">The selected owner type.</typeparam>
    /// <returns>The declared missing-owner options.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValidationObservationOptions<TValue> Suppress<TValue>() =>
        new(ValidationMissingOwnerPolicy.Suppress, null, EqualityComparer<TValue>.Default, false);

    /// <summary>Declares all ordinary object selections equivalent, including objects which expose state.</summary>
    private sealed class EqualObjectComparer : IEqualityComparer<object>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new bool Equals(object? x, object? y) => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(object obj) => 0;
    }

    /// <summary>A producer which deliberately retains observers after their cleanup is invoked.</summary>
    /// <param name="initial">The complete initial state delivered synchronously.</param>
    private sealed class NonCooperativeStates(IValidationState initial) : IObservable<IValidationState>
    {
        /// <summary>The observer retained even after disposal.</summary>
        private IObserver<IValidationState>? _observer;

        /// <summary>Gets the complete state emitted at subscription.</summary>
        public IValidationState Initial { get; } = initial;

        /// <summary>Gets the number of owned registrations.</summary>
        public int Subscriptions { get; private set; }

        /// <summary>Gets the number of cleanup invocations.</summary>
        public int Disposals { get; private set; }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<IValidationState> observer)
        {
            _observer = observer;
            Subscriptions++;
            observer.OnNext(Initial);
            return Disposable.Create(this, static source => source.Disposals++);
        }

        /// <summary>Emits a state even if the registration has been disposed.</summary>
        /// <param name="state">The deliberately late state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Emit(IValidationState state) => _observer?.OnNext(state);

        /// <summary>Emits an error even if the registration has been disposed.</summary>
        /// <param name="error">The deliberately late error.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fail(Exception error) => _observer?.OnError(error);
    }
}
