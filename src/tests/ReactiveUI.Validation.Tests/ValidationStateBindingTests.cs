// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reactive.Subjects;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Tests complete validation-state projections and their ownership.</summary>
public class ValidationStateBindingTests
{
    /// <summary>The invalid rule message.</summary>
    private const string ErrorMessage = "error";

    /// <summary>Verifies bool assignment reads validity even when invalid text is empty.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task HelperAssignsBooleanIndependentOfText()
    {
        using var model = new TestViewModel();
        using var states = new BehaviorSubject<IValidationState>(new ValidationState(false, ValidationText.None));
        using var helper = new ValidationHelper(new ObservableValidation<TestViewModel, IValidationState>(states));
        model.NameRule = helper;
        var view = new TestView(model);
        using var binding = view.BindValidationStateUnsafe(model, vm => vm.NameRule, v => v.IsNameValid, static state => state.IsValid);
        using var textBinding = view.BindValidationUnsafe(model, vm => vm!.NameRule, v => v.NameErrorLabel);
        await Assert.That(view.IsNameValid).IsFalse();
        await Assert.That(view.NameErrorLabel).IsEmpty();
        states.OnNext(ValidationState.Valid);
        await Assert.That(view.IsNameValid).IsTrue();
    }

    /// <summary>Verifies every matching rule contributes and native enum assignment uses a typed setter.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PropertyCombinesMixedRulesIntoNativeEnum()
    {
        using var model = new TestViewModel();
        using var first = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var second = new BehaviorSubject<IValidationState>(new ValidationState(false, ValidationText.None));
        using var firstRule = model.ValidationRule(vm => vm.Name, first);
        using var secondRule = model.ValidationRule(vm => vm.Name, second);
        var view = new StateView { ViewModel = model };
        using var binding = view.BindValidationStateUnsafe(
            model,
            vm => vm.Name,
            v => v.State,
            static states => states.All(static state => state.IsValid) ? DayOfWeek.Monday : DayOfWeek.Friday,
            true);
        await Assert.That(view.State).IsEqualTo(DayOfWeek.Friday);
        second.OnNext(ValidationState.Valid);
        await Assert.That(view.State).IsEqualTo(DayOfWeek.Monday);
        first.OnNext(new ValidationState(false, ValidationText.None));
        await Assert.That(view.State).IsEqualTo(DayOfWeek.Friday);
    }

    /// <summary>Verifies initially empty rules, membership changes, removal, and null models produce empty lists.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PropertyReportsEmptyAndChangingRuleMembership()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        var counts = new List<int>();
        using var binding = view.BindValidationStateUnsafe(model, vm => vm.Name, static states => states.Count, counts.Add, true);
        await Assert.That(counts[^1]).IsEqualTo(0);
        var rule = model.ValidationRuleUnsafe(vm => vm.Name, static _ => false, ErrorMessage);
        await Assert.That(counts[^1]).IsEqualTo(1);
        rule.Dispose();
        await Assert.That(counts[^1]).IsEqualTo(0);
        view.ViewModel = null;
        await Assert.That(counts[^1]).IsEqualTo(0);
    }

    /// <summary>Verifies replacement models detach old property state subscriptions and disposal is repeatable.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PropertySwitchesModelsAndStopsAfterDisposal()
    {
        using var first = new TestViewModel { Name = "first" };
        using var second = new TestViewModel { Name = "second" };
        using var firstRule = first.ValidationRuleUnsafe(vm => vm.Name, static value => value == "ok", ErrorMessage);
        using var secondRule = second.ValidationRuleUnsafe(vm => vm.Name, static value => value == "ok", ErrorMessage);
        var view = new TestView(first);
        var values = new List<bool>();
        var binding = view.BindValidationStateUnsafe(first, vm => vm.Name, static states => states.All(static state => state.IsValid), values.Add, true);
        view.ViewModel = second;
        var count = values.Count;
        first.Name = "ok";
        await Assert.That(values.Count).IsEqualTo(count);
        second.Name = "ok";
        await Assert.That(values[^1]).IsTrue();
        view.ViewModel = null;
        count = values.Count;
        second.Name = "bad";
        await Assert.That(values.Count).IsEqualTo(count);
        view.ViewModel = first;
        binding.Dispose();
        binding.Dispose();
        count = values.Count;
        first.Name = "bad";
        view.ViewModel = second;
        await Assert.That(values.Count).IsEqualTo(count);
    }

    /// <summary>Verifies helper/model replacement, null transitions, and callback disposal detach stale sources.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task HelperSwitchesModelsHelpersAndNulls()
    {
        using var first = new TestViewModel();
        using var second = new TestViewModel();
        using var oldStates = new BehaviorSubject<IValidationState>(new ValidationState(false, "old"));
        using var newStates = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var oldHelper = new ValidationHelper(new ObservableValidation<TestViewModel, IValidationState>(oldStates));
        using var newHelper = new ValidationHelper(new ObservableValidation<TestViewModel, IValidationState>(newStates));
        first.NameRule = oldHelper;
        second.NameRule = newHelper;
        var view = new TestView(first);
        var values = new List<bool>();
        var binding = view.BindValidationStateUnsafe(first, vm => vm.NameRule, static state => state.IsValid, values.Add);
        await Assert.That(values[^1]).IsFalse();
        first.NameRule = newHelper;
        await Assert.That(values[^1]).IsTrue();
        var count = values.Count;
        oldStates.OnNext(ValidationState.Valid);
        await Assert.That(values.Count).IsEqualTo(count);
        first.NameRule = null;
        count = values.Count;
        newStates.OnNext(new ValidationState(false, "new"));
        await Assert.That(values.Count).IsEqualTo(count);
        view.ViewModel = second;
        await Assert.That(values[^1]).IsFalse();
        view.ViewModel = null;
        await Assert.That(values[^1]).IsTrue();
        count = values.Count;
        newStates.OnNext(ValidationState.Valid);
        first.NameRule = oldHelper;
        await Assert.That(values.Count).IsEqualTo(count);
        view.ViewModel = second;
        binding.Dispose();
        binding.Dispose();
        count = values.Count;
        newStates.OnNext(new ValidationState(false, "disposed"));
        await Assert.That(values.Count).IsEqualTo(count);
    }

    /// <summary>Verifies a helper converter sees the original custom state object and nullable outputs work.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task CustomStateIdentityAndNullableProjectionArePreserved()
    {
        using var model = new TestViewModel();
        var custom = new CustomValidationState(false, string.Empty);
        using var states = new BehaviorSubject<IValidationState>(custom);
        using var helper = new ValidationHelper(new ObservableValidation<TestViewModel, IValidationState>(states));
        model.NameRule = helper;
        var view = new TestView(model);
        IValidationState? observed = null;
        using var identity = view.BindValidationStateUnsafe(model, vm => vm.NameRule, static state => state, state => observed = state);
        using var nullable = view.BindValidationStateUnsafe(
            model,
            vm => vm.NameRule,
            v => v.NameErrorLabel,
            static state => state.IsValid ? null : "invalid");
        await Assert.That(observed).IsSameReferenceAs(custom);
        await Assert.That(view.NameErrorLabel).IsEqualTo("invalid");
        states.OnNext(ValidationState.Valid);
        await Assert.That(view.NameErrorLabel).IsNull();
    }

    /// <summary>Verifies custom struct metadata reaches both helper and property converters without text-based deduplication.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task CustomStructMetadataChangesReachConverters()
    {
        const int updatedSeverity = 2;
        using var model = new TestViewModel();
        using var states = new BehaviorSubject<IValidationState>(new SeverityValidationState(1));
        using var helper = model.ValidationRule(vm => vm.Name, states);
        model.NameRule = helper;
        var view = new TestView(model);
        var helperValues = new List<int>();
        var propertyValues = new List<int>();
        using var helperBinding = view.BindValidationStateUnsafe(
            model,
            vm => vm.NameRule,
            static state => ((SeverityValidationState)state).Severity,
            helperValues.Add);
        using var propertyBinding = view.BindValidationStateUnsafe(
            model,
            vm => vm.Name,
            static values => ((SeverityValidationState)values[0]).Severity,
            propertyValues.Add,
            true);
        states.OnNext(new SeverityValidationState(updatedSeverity));
        await Assert.That(helperValues).IsEquivalentTo([1, updatedSeverity]);
        await Assert.That(propertyValues[^1]).IsEqualTo(updatedSeverity);
    }

    /// <summary>Verifies no synthetic valid state precedes active invalid rules.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PropertyDoesNotInventValidStateForActiveRules()
    {
        using var model = new TestViewModel();
        using var rule = model.ValidationRuleUnsafe(vm => vm.Name, static _ => false, ErrorMessage);
        var view = new TestView(model);
        var values = new List<bool>();
        using var binding = view.BindValidationStateUnsafe(model, vm => vm.Name, static states => states.All(static state => state.IsValid), values.Add, true);
        await Assert.That(values).IsNotEmpty();
        await Assert.That(values.TrueForAll(static value => !value)).IsTrue();
    }

    /// <summary>Verifies nested property hosts receive the latest typed value after replacement and null.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task TypedAssignmentFollowsNestedHostReplacement()
    {
        using var model = new TestViewModel();
        var rule = model.ValidationRuleUnsafe(vm => vm.Name, static _ => false, ErrorMessage);
        var view = new StateView { ViewModel = model };
        using var binding = view.BindValidationStateUnsafe(
            model,
            vm => vm.Name,
            v => v.Target!.IsValid,
            static states => states.All(static state => state.IsValid),
            true);
        await Assert.That(view.Target!.IsValid).IsFalse();
        var old = view.Target;
        view.Target = null;
        view.Target = new StateTarget { IsValid = true };
        await Assert.That(view.Target.IsValid).IsFalse();
        rule.Dispose();
        await Assert.That(view.Target.IsValid).IsTrue();
        await Assert.That(old.IsValid).IsFalse();
    }

    /// <summary>Verifies missing converters and callbacks fail at binding construction.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task NullArgumentsAreRejected()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        await Assert.That(() => view.BindValidationStateUnsafe(model, vm => vm.NameRule, (Func<IValidationState, bool>)null!, static _ => { }))
            .Throws<ArgumentNullException>();
        await Assert.That(() => view.BindValidationStateUnsafe(model, vm => vm.Name, static states => states.Count, (Action<int>)null!, true))
            .Throws<ArgumentNullException>();
        await Assert.That(() => view.BindValidationStateUnsafe(
            model,
            (Expression<Func<TestViewModel, string?>>)null!,
            v => v.IsNameValid,
            static states => states.All(static state => state.IsValid),
            true)).Throws<ArgumentNullException>();
    }
}
