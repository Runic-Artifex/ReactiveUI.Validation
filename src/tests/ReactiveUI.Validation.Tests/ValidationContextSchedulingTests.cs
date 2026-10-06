// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI.Primitives.Signals;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
#else
using ReactiveUI;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Protects synchronous domain state while presentation properties use a queued scheduler.</summary>
public class ValidationContextSchedulingTests
{
    /// <summary>The initial blocking rule message.</summary>
    private const string BlockingMessage = "Blocking rule";

    /// <summary>The changed message from an ongoing validator.</summary>
    private const string UpdatedMessage = "Updated error";

    /// <summary>Checks dynamic add/remove/bulk removal update admission and status before presentation drains.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task DynamicMembershipUpdatesCurrentStateAndCommandAdmissionInTheSameTurn()
    {
        var scheduler = new ValidationManualScheduler();
        using var context = new ValidationContext(scheduler);
        using var invalid = new ObservableValidation<object, bool>(new object(), Signal.Return(false), static value => value, BlockingMessage);
        using var otherInvalid = new ObservableValidation<object, bool>(new object(), Signal.Return(false), static value => value, "Other blocking rule");
        var states = new List<IValidationState>();
        var validity = new List<bool>();
        using var statesObservation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(context.ValidationStatusChange, states.Add);
        using var validityObservation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(context.Valid, validity.Add);
        using var command = ReactiveCommand.Create(static () => { }, context.Valid, scheduler);
        var gate = (ICommand)command;
        scheduler.Drain();
        await Assert.That(context.IsValid).IsTrue();
        await Assert.That(gate.CanExecute(null)).IsTrue();

        context.Add(invalid);
        await Assert.That(context.GetIsValid()).IsFalse();
        await Assert.That(context.Validations.Items).Contains(invalid);
        await Assert.That(validity[^1]).IsFalse();
        await Assert.That(states[^1].IsValid).IsFalse();
        await Assert.That(states[^1].Text).Contains(BlockingMessage);
        await Assert.That(gate.CanExecute(null)).IsFalse();
        await Assert.That(context.IsValid).IsTrue();
        scheduler.Drain();
        await Assert.That(context.IsValid).IsFalse();
        await Assert.That(context.Text).Contains(BlockingMessage);

        context.Remove(invalid);
        await Assert.That(context.GetIsValid()).IsTrue();
        await Assert.That(validity[^1]).IsTrue();
        await Assert.That(states[^1].IsValid).IsTrue();
        await Assert.That(states[^1].Text).IsEmpty();
        await Assert.That(gate.CanExecute(null)).IsTrue();
        await Assert.That(context.IsValid).IsFalse();
        scheduler.Drain();
        await Assert.That(context.IsValid).IsTrue();

        context.Add(invalid);
        context.Add(otherInvalid);
        scheduler.Drain();
        context.RemoveMany(context.Validations.Items.ToArray());
        await Assert.That(context.GetIsValid()).IsTrue();
        await Assert.That(context.Validations.Items).IsEmpty();
        await Assert.That(states[^1].IsValid).IsTrue();
        await Assert.That(states[^1].Text).IsEmpty();
        await Assert.That(gate.CanExecute(null)).IsTrue();
        scheduler.Drain();
        await Assert.That(context.Text).IsEmpty();
    }

    /// <summary>Checks an ongoing result stream and message-only changes use current aggregate validity rather than the queued property.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ObservableResultsPublishCoherentCurrentValidityAndTextBeforePresentationDrain()
    {
        var scheduler = new ValidationManualScheduler();
        using var results = new ReplaySignal<IValidationState>(1);
        results.OnNext(ValidationState.Valid);
        using var rule = new ObservableValidation<object, bool>(results);
        using var context = new ValidationContext(scheduler);
        context.Add(rule);
        var states = new List<IValidationState>();
        using var observation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(context.ValidationStatusChange, states.Add);
        scheduler.Drain();

        // A completed asynchronous validator marshals its result to the same model owner.
        results.OnNext(new ValidationState(false, ValidationText.Create("First error")));
        await Assert.That(context.GetIsValid()).IsFalse();
        await Assert.That(context.IsValid).IsTrue();
        await Assert.That(states[^1].IsValid).IsFalse();
        await Assert.That(states[^1].Text).Contains("First error");
        results.OnNext(new ValidationState(false, ValidationText.Create(UpdatedMessage)));
        await Assert.That(states[^1].IsValid).IsFalse();
        await Assert.That(states[^1].Text).Contains(UpdatedMessage);
        scheduler.Drain();
        await Assert.That(context.IsValid).IsFalse();
        await Assert.That(context.Text).Contains(UpdatedMessage);

        results.OnNext(ValidationState.Valid);
        await Assert.That(context.GetIsValid()).IsTrue();
        await Assert.That(states[^1].IsValid).IsTrue();
        await Assert.That(states[^1].Text).IsEmpty();
        await Assert.That(context.IsValid).IsFalse();
        scheduler.Drain();
        await Assert.That(context.IsValid).IsTrue();
        await Assert.That(context.Text).IsEmpty();
    }

    /// <summary>Checks disposing a context cancels pending presentation notifications without notifying raw observers again.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task DisposalBeforePresentationDrainDetachesPendingWork()
    {
        var scheduler = new ValidationManualScheduler();
        using var rule = new ObservableValidation<object, bool>(new object(), Signal.Return(false), static value => value, BlockingMessage);
        var context = new ValidationContext(scheduler);
        var states = new List<IValidationState>();
        using var observation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(context.ValidationStatusChange, states.Add);
        context.Add(rule);
        await Assert.That(states[^1].IsValid).IsFalse();
        context.Dispose();
        var count = states.Count;
        scheduler.Drain();
        await Assert.That(states.Count).IsEqualTo(count);
        await Assert.That(context.IsDisposed).IsTrue();
    }

    /// <summary>Checks a status observer can remove an invalid rule and all current streams finish at the surviving membership.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ReentrantRuleRemovalLeavesCurrentStreamsCoherent()
    {
        var scheduler = new ValidationManualScheduler();
        using var context = new ValidationContext(scheduler);
        using var rule = new ObservableValidation<object, bool>(new object(), Signal.Return(false), static value => value, BlockingMessage);
        var validity = new List<bool>();
        var states = new List<IValidationState>();
        using var validityObservation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(context.Valid, validity.Add);
        using var observation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(context.ValidationStatusChange, state =>
        {
            states.Add(state);
            if (!state.IsValid)
            {
                context.Remove(rule);
            }
        });
        scheduler.Drain();
        context.Add(rule);
        await Assert.That(context.GetIsValid()).IsTrue();
        await Assert.That(context.Validations.Items).IsEmpty();
        await Assert.That(validity[^1]).IsTrue();
        await Assert.That(states[^1].IsValid).IsTrue();
        scheduler.Drain();
        await Assert.That(context.IsValid).IsTrue();
    }
}
