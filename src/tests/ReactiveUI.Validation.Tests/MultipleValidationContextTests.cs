// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
using ImmediateContextScheduler = System.Reactive.Concurrency.ImmediateScheduler;
namespace ReactiveUI.Validation.Reactive.Tests;
#else
using ImmediateContextScheduler = ReactiveUI.Primitives.Concurrency.ImmediateSequencer;
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Regressions for explicit-context rules, observation and ownership.</summary>
public class MultipleValidationContextTests
{
    /// <summary>The first context message.</summary>
    private const string FirstMessage = "first";

    /// <summary>The replacement context message.</summary>
    private const string ReplacementMessage = "replacement";

    /// <summary>The second model message.</summary>
    private const string SecondMessage = "second";

    /// <summary>The number of selected observable and dynamic rule shapes.</summary>
    private const int SelectedRuleCount = 5;

    /// <summary>Invalid advice leaves the default command gate valid; invalid blocking rules disable it.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task AdviceAndBlockingContextsRemainIndependent()
    {
        using var model = new ContextModel { Name = "ok" };
        using var advice = new ValidationContext(ImmediateContextScheduler.Instance);
        using var warning = model.ValidationRule(advice, vm => vm.Name, static name => name == "excellent", "Improve the name");
        using var blocking = model.ValidationRule(vm => vm.Name, static name => !string.IsNullOrEmpty(name), "Name required");
        var saveEnabled = false;
        using var saveGate = model.ValidationContext.Valid.Subscribe(valid => saveEnabled = valid);
        await Assert.That(saveEnabled).IsTrue();
        await Assert.That(advice.GetIsValid()).IsFalse();
        model.Name = string.Empty;
        await Assert.That(saveEnabled).IsFalse();
        advice.RemoveMany(advice.Validations.Items.ToArray());
        await Assert.That(model.ValidationContext.GetIsValid()).IsFalse();
        await Assert.That(advice.GetIsValid()).IsTrue();
        model.Name = "ok";
        await Assert.That(saveEnabled).IsTrue();
    }

    /// <summary>Helpers retain the exact destination after a model's context property is replaced.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task HelperCleanupCapturesContextAndOwnsOnlyItsRule()
    {
        using var model = new ContextModel();
        using var first = new ValidationContext(ImmediateContextScheduler.Instance);
        using var replacement = new ValidationContext(ImmediateContextScheduler.Instance);
        model.Advice = first;
        using var retained = model.ValidationRule(first, vm => vm.Name, static _ => false, "retained");
        var removed = model.ValidationRule(model.Advice, vm => vm.Name, static _ => false, "removed");
        using var other = model.ValidationRule(replacement, vm => vm.Name, static _ => false, "other");
        model.Advice = replacement;
        removed.Dispose();
        removed.Dispose();
        await Assert.That(first.Validations.Count).IsEqualTo(1);
        await Assert.That(replacement.Validations.Count).IsEqualTo(1);
        await Assert.That(first.IsDisposed).IsFalse();
        await Assert.That(replacement.IsDisposed).IsFalse();
        await Assert.That(model.ValidationContext.Validations.Count).IsEqualTo(0);
    }

    /// <summary>Every selected rule shape registers only in its explicit destination.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task ObservableAndDynamicMessageShapesRegisterInSelectedContext()
    {
        using var model = new ContextModel { Name = "short" };
        using var advice = new ValidationContext(ImmediateContextScheduler.Instance);
        using var booleans = new BehaviorSubject<bool>(false);
        using var states = new BehaviorSubject<IValidationState>(new ValidationState(false, "state"));
        using var dynamicRule = model.ValidationRule(advice, vm => vm.Name, static value => value == "long", static value => $"Improve {value}");
        using var wholeBool = model.ValidationRule(advice, booleans, "whole bool");
        using var propertyBool = model.ValidationRule(advice, vm => vm.Name, booleans, "property bool");
        using var wholeState = model.ValidationRule(advice, states);
        using var propertyState = model.ValidationRule(advice, vm => vm.Name, states);
        await Assert.That(advice.Validations.Count).IsEqualTo(SelectedRuleCount);
        await Assert.That(model.ValidationContext.GetIsValid()).IsTrue();
        await Assert.That(advice.GetIsValid()).IsFalse();
        booleans.OnNext(true);
        states.OnNext(ValidationState.Valid);
        model.Name = "long";
        await Assert.That(advice.GetIsValid()).IsTrue();
    }

    /// <summary>Whole and property text/action bindings follow context and model replacement and null.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task BindingsSwitchContextAndViewModelAndClearNull()
    {
        using var firstModel = new ContextModel();
        using var secondModel = new ContextModel();
        using var first = new ValidationContext(ImmediateContextScheduler.Instance);
        using var replacement = new ValidationContext(ImmediateContextScheduler.Instance);
        using var second = new ValidationContext(ImmediateContextScheduler.Instance);
        using var firstValues = new BehaviorSubject<bool>(false);
        using var replacementValues = new BehaviorSubject<bool>(false);
        using var secondValues = new BehaviorSubject<bool>(false);
        using var firstRule = firstModel.ValidationRule(first, vm => vm.Name, firstValues, FirstMessage);
        using var replacementRule = firstModel.ValidationRule(replacement, vm => vm.Name, replacementValues, ReplacementMessage);
        using var secondRule = secondModel.ValidationRule(second, vm => vm.Name, secondValues, SecondMessage);
        firstModel.Advice = first;
        secondModel.Advice = second;
        var view = new ContextView { ViewModel = firstModel };
        var wholeMessage = string.Empty;
        var propertyMessage = string.Empty;
        using var wholeText = view.BindValidationContext(firstModel, vm => vm.Advice, v => v.WholeText);
        using var propertyText = view.BindValidationContext(firstModel, vm => vm.Advice, vm => vm.Name, v => v.PropertyText);
        using var wholeAction = view.BindValidationContext(firstModel, vm => vm.Advice, state => wholeMessage = state.Text.ToSingleLine());
        using var propertyAction = view.BindValidationContext(
            firstModel,
            vm => vm.Advice,
            vm => vm.Name,
            states => propertyMessage = states.Count == 0 ? string.Empty : states[states.Count - 1].Text.ToSingleLine());
        await Assert.That(view.WholeText).IsEqualTo(FirstMessage);
        await Assert.That(view.PropertyText).IsEqualTo(FirstMessage);
        firstModel.Advice = replacement;
        await Assert.That(view.WholeText).IsEqualTo(ReplacementMessage);
        firstValues.OnNext(true);
        await Assert.That(view.WholeText).IsEqualTo(ReplacementMessage);
        await Assert.That(view.PropertyText).IsEqualTo(ReplacementMessage);
        await Assert.That(wholeMessage).IsEqualTo(ReplacementMessage);
        await Assert.That(propertyMessage).IsEqualTo(ReplacementMessage);
        firstModel.Advice = null;
        await Assert.That(view.WholeText).IsEqualTo(string.Empty);
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
        replacementValues.OnNext(true);
        await Assert.That(wholeMessage).IsEqualTo(string.Empty);
        firstModel.Advice = replacement;
        replacementValues.OnNext(false);
        view.ViewModel = secondModel;
        await Assert.That(view.WholeText).IsEqualTo(SecondMessage);
        firstModel.Advice = first;
        replacementValues.OnNext(true);
        await Assert.That(view.PropertyText).IsEqualTo(SecondMessage);
        view.ViewModel = null;
        await Assert.That(view.WholeText).IsEqualTo(string.Empty);
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
        await Assert.That(wholeMessage).IsEqualTo(string.Empty);
        await Assert.That(propertyMessage).IsEqualTo(string.Empty);
        secondValues.OnNext(true);
        await Assert.That(view.WholeText).IsEqualTo(string.Empty);
        view.ViewModel = secondModel;
        secondValues.OnNext(false);
        await Assert.That(view.PropertyText).IsEqualTo(SecondMessage);
    }

    /// <summary>A selected context can be null before any view model is available.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task InitiallyNullModelAndContextAreValidEmptySelections()
    {
        using var model = new ContextModel();
        var view = new ContextView();
        var valid = false;
        using var binding = view.BindValidationContext(model, vm => vm.Advice, state => valid = state.IsValid);
        using var text = view.BindValidationContext(model, vm => vm.Advice, vm => vm.Name, v => v.PropertyText);
        await Assert.That(valid).IsTrue();
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
        view.ViewModel = model;
        await Assert.That(valid).IsTrue();
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
    }

    /// <summary>Switching from an error to an initially empty context clears text and observes late rules.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task EmptySelectedContextClearsMessagesAndObservesLateRules()
    {
        using var model = new ContextModel();
        using var invalid = new ValidationContext(ImmediateContextScheduler.Instance);
        using var empty = new ValidationContext(ImmediateContextScheduler.Instance);
        using var rule = model.ValidationRule(invalid, vm => vm.Name, static _ => false, FirstMessage);
        model.Advice = invalid;
        var view = new ContextView { ViewModel = model, PropertyText = "stale" };
        using var binding = view.BindValidationContext(model, vm => vm.Advice, vm => vm.Name, v => v.PropertyText);
        await Assert.That(view.PropertyText).IsEqualTo(FirstMessage);
        model.Advice = empty;
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
        var lateRule = model.ValidationRule(empty, vm => vm.Name, static _ => false, SecondMessage);
        await Assert.That(view.PropertyText).IsEqualTo(SecondMessage);
        lateRule.Dispose();
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
        view.PropertyText = "initial stale";
        using var secondBinding = view.BindValidationContext(model, vm => vm.Advice, vm => vm.Name, v => v.PropertyText);
        await Assert.That(view.PropertyText).IsEqualTo(string.Empty);
    }

    /// <summary>Disposing text and action bindings releases subscriptions without owning rules or contexts.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task BindingsDisposeRepeatedlyWithoutDisposingContextOrRule()
    {
        using var model = new ContextModel();
        using var context = new ValidationContext(ImmediateContextScheduler.Instance);
        using var values = new BehaviorSubject<bool>(false);
        using var rule = model.ValidationRule(context, vm => vm.Name, values, FirstMessage);
        model.Advice = context;
        var view = new ContextView { ViewModel = model };
        var message = string.Empty;
        var text = view.BindValidationContext(model, vm => vm.Advice, vm => vm.Name, v => v.PropertyText);
        var action = view.BindValidationContext(model, vm => vm.Advice, state => message = state.Text.ToSingleLine());
        text.Dispose();
        action.Dispose();
        text.Dispose();
        action.Dispose();
        values.OnNext(true);
        await Assert.That(view.PropertyText).IsEqualTo(FirstMessage);
        await Assert.That(message).IsEqualTo(FirstMessage);
        await Assert.That(context.IsDisposed).IsFalse();
        await Assert.That(context.Validations.Count).IsEqualTo(1);
    }

    /// <summary>A helper still releases its observable if its destination was already disposed.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task HelperReleasesRuleAfterDestinationDisposal()
    {
        using var model = new ContextModel();
        using var values = new BehaviorSubject<bool>(false);
        var context = new ValidationContext(ImmediateContextScheduler.Instance);
        var helper = model.ValidationRule(context, values, FirstMessage);
        await Assert.That(values.HasObservers).IsTrue();
        context.Dispose();
        helper.Dispose();
        helper.Dispose();
        await Assert.That(values.HasObservers).IsFalse();
        await Assert.That(model.ValidationContext.GetIsValid()).IsTrue();
    }

    /// <summary>A callback can dispose its own binding and prevent later notifications.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task ActionBindingSupportsReentrantDisposal()
    {
        using var model = new ContextModel();
        using var context = new ValidationContext(ImmediateContextScheduler.Instance);
        using var values = new BehaviorSubject<bool>(true);
        using var rule = model.ValidationRule(context, values, FirstMessage);
        model.Advice = context;
        var view = new ContextView { ViewModel = model };
        IValidationBinding? binding = null;
        var notifications = 0;
        binding = view.BindValidationContext(model, vm => vm.Advice, state =>
        {
            notifications++;
            if (!state.IsValid)
            {
                binding!.Dispose();
                binding.Dispose();
            }
        });
        values.OnNext(false);
        var afterDisposal = notifications;
        values.OnNext(true);
        await Assert.That(notifications).IsEqualTo(afterDisposal);
        await Assert.That(context.GetIsValid()).IsTrue();
        binding.Dispose();
    }

    /// <summary>Explicit destinations and context selectors are required.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task NullArgumentsFailBeforeRegistrationOrSubscription()
    {
        using var model = new ContextModel();
        using var context = new ValidationContext(ImmediateContextScheduler.Instance);
        var view = new ContextView { ViewModel = model };
        await Assert.That(() => model.ValidationRule(null!, vm => vm.Name, static _ => false, FirstMessage)).Throws<ArgumentNullException>();
        await Assert.That(() => model.ValidationRule(context, vm => vm.Name, (Func<string?, bool>)null!, FirstMessage)).Throws<ArgumentNullException>();
        await Assert.That(() => view.BindValidationContext(model, null!, static _ => { })).Throws<ArgumentNullException>();
        await Assert.That(() => view.BindValidationContext(model, vm => vm.Advice, (Action<IValidationState>)null!)).Throws<ArgumentNullException>();
        await Assert.That(context.Validations.Count).IsEqualTo(0);
        await Assert.That(model.ValidationContext.Validations.Count).IsEqualTo(0);
    }

    /// <summary>Populated invalid contexts never emit a synthetic empty or valid property state list.</summary>
    /// <returns>The asynchronous test task.</returns>
    [Test]
    public async Task PropertyActionsReceiveActualInvalidStatesWithoutValidPrelude()
    {
        using var model = new ContextModel();
        using var context = new ValidationContext(ImmediateContextScheduler.Instance);
        using var rule = model.ValidationRule(context, vm => vm.Name, static _ => false, FirstMessage);
        model.Advice = context;
        var view = new ContextView { ViewModel = model };
        List<IList<IValidationState>> received = [];
        using var binding = view.BindValidationContext(model, vm => vm.Advice, vm => vm.Name, received.Add);
        await Assert.That(received.Count).IsGreaterThan(0);
        await Assert.That(received.TrueForAll(static states => states.Count == 1 && !states[0].IsValid)).IsTrue();
        model.Advice = null;
        await Assert.That(received[received.Count - 1].Count).IsEqualTo(0);
    }

    /// <summary>A view model with independently owned contexts and a replaceable advice selection.</summary>
    private sealed class ContextModel : ReactiveObject, IValidatableViewModel, IDisposable
    {
        /// <summary>Gets or sets the validated property.</summary>
        public string? Name
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        /// <summary>Gets or sets the selected advisory context; callers own its lifetime.</summary>
        public IValidationContext? Advice
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        /// <inheritdoc/>
        public IValidationContext ValidationContext { get; } = new ValidationContext(ImmediateContextScheduler.Instance);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => ValidationContext.Dispose();
    }

    /// <summary>A view with two independently bound string properties.</summary>
    private sealed class ContextView : ReactiveObject, IViewFor<ContextModel>
    {
        /// <inheritdoc/>
        public ContextModel? ViewModel
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        /// <inheritdoc/>
        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value as ContextModel;
        }

        /// <summary>Gets or sets the aggregate context message.</summary>
        public string WholeText { get; set; } = string.Empty;

        /// <summary>Gets or sets the property-specific message.</summary>
        public string PropertyText { get; set; } = string.Empty;
    }
}
