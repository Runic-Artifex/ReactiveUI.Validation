// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

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

/// <summary>Verifies ordinal legacy compatibility without weakening structural property identity.</summary>
[ValidationRuntimeDispatch]
public class CapabilityPathCompatibilityTests
{
    /// <summary>The field's complete legacy presentation name.</summary>
    private const string FieldPath = "_nameField";

    /// <summary>A different complete path with the same leaf name.</summary>
    private const string NestedPath = "Owner._nameField";

    /// <summary>A case-distinct legacy name.</summary>
    private const string DifferentCasePath = "_namefield";

    /// <summary>An unrelated complete legacy path.</summary>
    private const string UnrelatedPath = "OtherField";

    /// <summary>The complete state text retained through matching.</summary>
    private const string ErrorMessage = "field error";

    /// <summary>The bounded descriptor and attachment capacity.</summary>
    private const int RegistryCapacity = 2;

    /// <summary>The actual number of selected slots in the multipath component.</summary>
    private const int MultiplePathCount = 2;

    /// <summary>A registered normal field binding includes an existing string-metadata observable rule.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task NormalFieldBindingMatchesLegacyObservableRuleMetadata()
    {
        using var model = new FieldModel();
        var view = new FieldView(model);
        var initial = new ValidationState(false, ErrorMessage);
        using var states = new BehaviorSubject<IValidationState>(initial);
        using var rule = model.AddObservableRule(states, [FieldPath]);
        var expression = FieldModel.SelectField();
        var fieldPath = StructuralFieldPath();
        var descriptor = new ValidationSelector<FieldModel, string>(_ => new(
            static () => throw new InvalidOperationException("Property matching must only read path metadata."),
            [],
            ValidationObservationOptions<string>.Default,
            () => [fieldPath]));
        using var registry = new ValidationPlanRegistry(RegistryCapacity);
        using var attachment = registry.Attach(view);
        using var registration = registry.RegisterSelector(ValidationPlanRole.Property, expression, descriptor);
        var observed = new List<IList<IValidationState>>();
        using var binding = view.BindValidationState(model, expression, static current => current, observed.Add, true);
        await Assert.That(observed[^1].Count).IsEqualTo(1);
        await Assert.That(observed[^1][0]).IsSameReferenceAs(initial);
        states.OnNext(ValidationState.Valid);
        await Assert.That(observed[^1][0]).IsSameReferenceAs(ValidationState.Valid);
    }

    /// <summary>A typed component can deliberately retain legacy metadata and match a structural query.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task TypedStoredLegacyPathMatchesStructuralQuery()
    {
        using var context = new ValidationContext(ImmediateSequencer.Instance);
        var state = new ValidationState(false, ErrorMessage);
        using var rule = TypedRule([ValidationPath.Legacy(FieldPath)], state);
        context.Add(rule);
        var query = StructuralFieldPath();
        IList<IValidationState>? observed = null;
        using var subscription = Observe(context, query, true).Subscribe(current => observed = current);
        await Assert.That(rule.ContainsPath(query, true)).IsTrue();
        await Assert.That(observed!.Count).IsEqualTo(1);
        await Assert.That(observed[0]).IsSameReferenceAs(state);
    }

    /// <summary>Structural metadata never falls back to display text when declaring, member or index identities differ.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task SameDisplayStructuralPathsRequireExactIdentity()
    {
        using var context = new ValidationContext(ImmediateSequencer.Instance);
        var selected = StructuralFieldPath();
        var state = new ValidationState(false, ErrorMessage);
        using var rule = TypedRule([selected], state);
        context.Add(rule);
        var wrongDeclaring = ValidationPath.Structural(FieldPath, (Declaring: 1, Member: 0, Index: 0), EqualityComparer<(int Declaring, int Member, int Index)>.Default);
        var wrongMember = ValidationPath.Structural(FieldPath, (Declaring: 0, Member: 1, Index: 0), EqualityComparer<(int Declaring, int Member, int Index)>.Default);
        var wrongIndex = ValidationPath.Structural(FieldPath, (Declaring: 0, Member: 0, Index: 1), EqualityComparer<(int Declaring, int Member, int Index)>.Default);
        await Assert.That(rule.ContainsPath(selected, true)).IsTrue();
        await Assert.That(rule.ContainsPath(wrongDeclaring, false)).IsFalse();
        await Assert.That(rule.ContainsPath(wrongMember, false)).IsFalse();
        await Assert.That(rule.ContainsPath(wrongIndex, false)).IsFalse();
        IList<IValidationState>? observed = null;
        using var subscription = Observe(context, wrongIndex, false).Subscribe(current => observed = current);
        await Assert.That(observed!.Count).IsEqualTo(0);
    }

    /// <summary>Legacy compatibility uses complete ordinal paths and the actual number of selected slots for strictness.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task LegacyCompatibilityRetainsFullPathCaseAndStrictExclusion()
    {
        using var context = new ValidationContext(ImmediateSequencer.Instance);
        var state = new ValidationState(false, ErrorMessage);
        var otherSlot = ValidationPath.Structural(FieldPath, (Declaring: 0, Member: 0, Index: 1), EqualityComparer<(int Declaring, int Member, int Index)>.Default);
        using var typed = TypedRule([ValidationPath.Legacy(FieldPath), otherSlot], state);
        using var nested = context.AddObservableRule(Observable.Return<IValidationState>(state), [NestedPath]);
        context.Add(typed);
        var query = StructuralFieldPath();
        await Assert.That(typed.PropertyCount).IsEqualTo(MultiplePathCount);
        await Assert.That(typed.ContainsPath(query, false)).IsTrue();
        await Assert.That(typed.ContainsPath(query, true)).IsFalse();
        IList<IValidationState>? relaxedStates = null;
        IList<IValidationState>? strictStates = null;
        IList<IValidationState>? nestedStates = null;
        IList<IValidationState>? caseStates = null;
        IList<IValidationState>? unrelatedStates = null;
        using var relaxed = Observe(context, query, false).Subscribe(current => relaxedStates = current);
        using var strict = Observe(context, query, true).Subscribe(current => strictStates = current);
        using var full = Observe(context, ValidationPath.Structural(NestedPath, 0, EqualityComparer<int>.Default), true)
            .Subscribe(current => nestedStates = current);
        using var differentCase = Observe(context, ValidationPath.Structural(DifferentCasePath, 0, EqualityComparer<int>.Default), false)
            .Subscribe(current => caseStates = current);
        using var unrelated = Observe(context, ValidationPath.Structural(UnrelatedPath, 0, EqualityComparer<int>.Default), false)
            .Subscribe(current => unrelatedStates = current);
        await Assert.That(relaxedStates!.Count).IsEqualTo(1);
        await Assert.That(relaxedStates[0]).IsSameReferenceAs(state);
        await Assert.That(strictStates!.Count).IsEqualTo(0);
        await Assert.That(nestedStates!.Count).IsEqualTo(1);
        await Assert.That(nestedStates[0]).IsSameReferenceAs(state);
        await Assert.That(caseStates!.Count).IsEqualTo(0);
        await Assert.That(unrelatedStates!.Count).IsEqualTo(0);
        using var multipleLegacy = context.AddObservableRule(Observable.Return<IValidationState>(state), [FieldPath, UnrelatedPath]);
        await Assert.That(relaxedStates.Count).IsEqualTo(MultiplePathCount);
        await Assert.That(strictStates.Count).IsEqualTo(0);
    }

    /// <summary>Creates a stable member identity domain for the selected field.</summary>
    /// <returns>The structural field path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValidationPath StructuralFieldPath() => ValidationPath.Structural(
        FieldPath,
        (Declaring: 0, Member: 0, Index: 0),
        EqualityComparer<(int Declaring, int Member, int Index)>.Default);

    /// <summary>Creates a typed component with explicitly chosen path metadata.</summary>
    /// <param name="paths">The complete slot metadata.</param>
    /// <param name="state">The caller-created complete validation state.</param>
    /// <returns>The independently disposable component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SelectorValidation<object, bool> TypedRule(IReadOnlyList<ValidationPath> paths, IValidationState state) => new(
        new(),
        new(_ => new(() => ValidationRead<bool>.Present(false, paths), [], ValidationObservationOptions<bool>.Default)),
        _ => state);

    /// <summary>Observes a structural query against current context rule membership.</summary>
    /// <param name="context">The borrowed validation context.</param>
    /// <param name="path">The selected identity.</param>
    /// <param name="strict">Whether only single-slot rules are included.</param>
    /// <returns>The matching complete states.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IList<IValidationState>> Observe(ValidationContext context, ValidationPath path, bool strict) => ValidationRuntime.ObserveProperty(
        context,
        new(source => new(() => ValidationRead<IValidationContext?>.Present(source, []), [], ValidationObservationOptions<IValidationContext?>.Default)),
        new(_ => new(() => ValidationRead<ValidationPath>.Present(path, []), [], ValidationObservationOptions<ValidationPath>.Default)),
        strict,
        ValidationInitialSequence.Actual);

    /// <summary>A validatable model with a real field selection.</summary>
    private sealed class FieldModel : ReactiveObject, IValidatableViewModel, IDisposable
    {
        /// <summary>The selected field whose value is unnecessary for property matching.</summary>
        private readonly string _nameField = ErrorMessage;

        /// <inheritdoc/>
        public IValidationContext ValidationContext { get; } = new ValidationContext(ImmediateSequencer.Instance);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => ValidationContext.Dispose();

        /// <summary>Returns the authored field expression within its legal private access scope.</summary>
        /// <returns>The expression selecting the physical field.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Expression<Func<FieldModel, string>> SelectField() => source => source._nameField;
    }

    /// <summary>A typed notifying view providing the normal binding's original ABI.</summary>
    /// <param name="model">The initially selected model.</param>
    private sealed class FieldView(FieldModel model) : ReactiveObject, IViewFor<FieldModel>
    {
        /// <inheritdoc/>
        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value as FieldModel;
        }

        /// <inheritdoc/>
        public FieldModel? ViewModel { get; set; } = model;
    }
}
