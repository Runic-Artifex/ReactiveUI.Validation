// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Regressions for explicit observables, property metadata, and typed callbacks.</summary>
public class ObservableRuntimeApiTests
{
    /// <summary>The complete nested identity.</summary>
    private const string NestedPath = "Editor.Name";

    /// <summary>The number of broad matches.</summary>
    private const int BroadMatchCount = 2;

    /// <summary>The changed custom metadata value.</summary>
    private const int UpdatedSeverity = 2;

    /// <summary>Verifies value projection uses the captured default owner without requiring a reactive model.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task DefaultRegistrationCapturesContextAndReleasesSubscription()
    {
        using var original = new ValidationContext();
        using var replacement = new ValidationContext();
        using var values = new BehaviorSubject<int>(0);
        var model = new PlainModel { ValidationContext = original };
        var helper = model.AddObservableRule(values, static value => new ValidationState(value > 0, "error"), [NestedPath]);
        await Assert.That(original.GetIsValid()).IsFalse();
        await Assert.That(values.HasObservers).IsTrue();
        model.ValidationContext = replacement;
        var unrelated = replacement.AddObservableRule(values, static value => new ValidationState(value > 0, "replacement"), []);
        values.OnNext(1);
        await Assert.That(original.GetIsValid()).IsTrue();
        helper.Dispose();
        helper.Dispose();
        await Assert.That(original.Validations.Count).IsEqualTo(0);
        await Assert.That(replacement.Validations.Count).IsEqualTo(1);
        unrelated.Dispose();
        await Assert.That(values.HasObservers).IsFalse();
    }

    /// <summary>Verifies exact full paths, duplicate identities, strict matching, and initial invalid states.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task FullPathsAndStrictExclusivitySeedActualStates()
    {
        using var context = new ValidationContext();
        using var states = new BehaviorSubject<IValidationState>(new ValidationState(false, ValidationText.None));
        using var exclusive = context.AddObservableRule(states, [NestedPath, NestedPath]);
        using var shared = context.AddObservableRule(states, [NestedPath, "Editor.Title"]);
        using var other = context.AddObservableRule(states, ["Other.Name"]);
        using var modelWide = context.AddObservableRule(states, []);
        using var selected = new BehaviorSubject<IValidationContext?>(context);
        var strictValues = new List<bool>();
        var broadCounts = new List<int>();
        var shortCounts = new List<int>();
        using var strict = selected.BindObservablePropertyValidationState(static value => value, NestedPath, static values => values.Count == 1 && !values[0].IsValid, strictValues.Add, true);
        using var broad = selected.BindObservablePropertyValidationState(static value => value, NestedPath, static values => values.Count, broadCounts.Add, false);
        using var shortName = selected.BindObservablePropertyValidationState(static value => value, "Name", static values => values.Count, shortCounts.Add, false);
        await Assert.That(strictValues.TrueForAll(static value => value)).IsTrue();
        await Assert.That(broadCounts[^1]).IsEqualTo(BroadMatchCount);
        await Assert.That(shortCounts[^1]).IsEqualTo(0);
        var component = (IPropertyValidationComponent)context.Validations.Items[0];
        await Assert.That(component.PropertyCount).IsEqualTo(1);
        await Assert.That(component.Properties.Single()).IsEqualTo(NestedPath);
    }

    /// <summary>Verifies class identity, boxed struct metadata, duplicate text, and invalid empty messages survive projection.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task RawStatesRetainMixedMetadataAndEveryEmission()
    {
        using var context = new ValidationContext();
        var first = new CustomValidationState(false, string.Empty);
        using var classStates = new BehaviorSubject<IValidationState>(first);
        using var structStates = new BehaviorSubject<IValidationState>(new SeverityValidationState(1));
        using var classRule = context.AddObservableRule(classStates, ["Name"]);
        using var structRule = context.AddObservableRule(structStates, ["Name"]);
        using var selected = new BehaviorSubject<IValidationContext?>(context);
        var snapshots = new List<IList<IValidationState>>();
        using var property = selected.BindObservablePropertyValidationState(static value => value, "Name", static values => values, snapshots.Add, true);
        using var helpers = new BehaviorSubject<ValidationHelper?>(classRule);
        var identities = new List<IValidationState>();
        using var helper = helpers.BindObservableValidationState(static value => value.ValidationChanged, static value => value, identities.Add);
        var second = new CustomValidationState(false, string.Empty);
        classStates.OnNext(second);
        classStates.OnNext(second);
        structStates.OnNext(new SeverityValidationState(UpdatedSeverity));
        await Assert.That(identities).IsEquivalentTo(new IValidationState[] { first, second, second });
        await Assert.That(snapshots[^1][0]).IsSameReferenceAs(second);
        await Assert.That(((SeverityValidationState)snapshots[^1][1]).Severity).IsEqualTo(UpdatedSeverity);
        await Assert.That(snapshots[^1].All(static value => !value.IsValid && value.Text.ToSingleLine().Length == 0)).IsTrue();
    }

    /// <summary>Verifies initial empty membership, addition/removal, replaced contexts, null selections, and disposal.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PropertyBindingTracksMembershipContextReplacementAndNull()
    {
        using var original = new ValidationContext();
        using var replacement = new ValidationContext();
        using var oldStates = new BehaviorSubject<IValidationState>(new ValidationState(false, "old"));
        using var newStates = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var sources = new BehaviorSubject<PlainModel?>(new PlainModel { ValidationContext = original });
        var counts = new List<int>();
        var binding = sources.BindObservablePropertyValidationState(static model => model.ValidationContext, "Name", static values => values.Count, counts.Add, true);
        await Assert.That(counts[^1]).IsEqualTo(0);
        var originalRule = original.AddObservableRule(oldStates, ["Name"]);
        await Assert.That(counts[^1]).IsEqualTo(1);
        var replacementRule = replacement.AddObservableRule(newStates, ["Name"]);
        sources.OnNext(new PlainModel { ValidationContext = replacement });
        var count = counts.Count;
        oldStates.OnNext(ValidationState.Valid);
        originalRule.Dispose();
        await Assert.That(counts.Count).IsEqualTo(count);
        replacementRule.Dispose();
        await Assert.That(counts[^1]).IsEqualTo(0);
        sources.OnNext(null);
        count = counts.Count;
        using var lateRule = replacement.AddObservableRule(newStates, ["Name"]);
        await Assert.That(counts.Count).IsEqualTo(count);
        sources.OnNext(new PlainModel { ValidationContext = replacement });
        binding.Dispose();
        binding.Dispose();
        count = counts.Count;
        newStates.OnNext(new ValidationState(false, "stale"));
        sources.OnNext(null);
        await Assert.That(counts.Count).IsEqualTo(count);
    }

    /// <summary>Verifies helper and context replacement detach old sources and null stream selections emit valid.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task StateBindingSwitchesHelpersContextsAndNullableProjection()
    {
        using var context = new ValidationContext();
        using var oldStates = new BehaviorSubject<IValidationState>(new ValidationState(false, "old"));
        using var newStates = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var oldHelper = context.AddObservableRule(oldStates, []);
        using var newHelper = context.AddObservableRule(newStates, []);
        using var sources = new BehaviorSubject<ValidationHelper?>(oldHelper);
        var values = new List<DayOfWeek?>();
        var binding = sources.BindObservableValidationState(static helper => helper.ValidationChanged, static state => state.IsValid ? (DayOfWeek?)null : DayOfWeek.Friday, values.Add);
        await Assert.That(values[^1]).IsEqualTo(DayOfWeek.Friday);
        sources.OnNext(newHelper);
        await Assert.That(values[^1]).IsNull();
        var count = values.Count;
        oldStates.OnNext(ValidationState.Valid);
        await Assert.That(values.Count).IsEqualTo(count);
        sources.OnNext(null);
        count = values.Count;
        newStates.OnNext(new ValidationState(false, "new"));
        await Assert.That(values.Count).IsEqualTo(count);
        sources.OnNext(newHelper);
        await Assert.That(values[^1]).IsEqualTo(DayOfWeek.Friday);
        binding.Dispose();
        binding.Dispose();
        count = values.Count;
        newStates.OnNext(ValidationState.Valid);
        await Assert.That(values.Count).IsEqualTo(count);
        using var contexts = new BehaviorSubject<IValidationContext?>(context);
        var validity = new List<bool>();
        using var selected = contexts.BindObservableValidationState(static current => current.ValidationStatusChange, static state => state.IsValid, validity.Add);
        contexts.OnNext(null);
        await Assert.That(validity[^1]).IsTrue();
        using var missing = contexts.BindObservableValidationState(static _ => null, static state => state.IsValid, validity.Add);
        await Assert.That(validity[^1]).IsTrue();
    }

    /// <summary>Verifies domain projections remain synchronous despite queued presentation and reentrant binding disposal.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SynchronousOwnerAndReentrantDisposalStopFurtherCallbacks()
    {
        var scheduler = new ValidationManualScheduler();
        using var context = new ValidationContext(scheduler);
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var rule = context.AddObservableRule(states, ["Name"]);
        using var sources = new BehaviorSubject<IValidationContext?>(context);
        var callbacks = new List<bool>();
        IValidationBinding? binding = null;
        binding = sources.BindObservablePropertyValidationState(
            static value => value,
            "Name",
            static values => values.All(static state => state.IsValid),
            value =>
        {
            callbacks.Add(value);
            if (!value)
            {
                binding!.Dispose();
                binding.Dispose();
            }
            },
            true);
        states.OnNext(new ValidationState(false, ValidationText.None));
        await Assert.That(callbacks[^1]).IsFalse();
        await Assert.That(context.GetIsValid()).IsFalse();
        var count = callbacks.Count;
        states.OnNext(ValidationState.Valid);
        scheduler.Drain();
        await Assert.That(callbacks.Count).IsEqualTo(count);
        binding.Dispose();
    }

    /// <summary>Verifies reentrant rule removal disposes the owned connection once without disposing the caller's observable.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ReentrantHelperCleanupUsesCapturedOwner()
    {
        using var context = new ValidationContext();
        using var states = new BehaviorSubject<IValidationState>(new ValidationState(false, "invalid"));
        var helper = context.AddObservableRule(states, ["Name"]);
        using var observations = context.ValidationStatusChange.Subscribe(state =>
        {
            if (state.IsValid)
            {
                helper.Dispose();
            }
        });
        helper.Dispose();
        await Assert.That(context.Validations.Count).IsEqualTo(0);
        await Assert.That(states.HasObservers).IsFalse();
        states.OnNext(ValidationState.Valid);
        helper.Dispose();
    }

    /// <summary>Verifies malformed paths fail before registration or source connection.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InvalidArgumentsFailBeforeRegistration()
    {
        using var context = new ValidationContext();
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        foreach (var path in new[] { string.Empty, ".Name", "Name.", "Editor..Name", "Editor. Name" })
        {
            await Assert.That(() => context.AddObservableRule(states, [path])).Throws<ArgumentException>();
        }

        await Assert.That(() => context.AddObservableRule(states, [null!])).Throws<ArgumentNullException>();
        await Assert.That(() => context.AddObservableRule(states, null!)).Throws<ArgumentNullException>();
        await Assert.That(context.Validations.Count).IsEqualTo(0);
        await Assert.That(states.HasObservers).IsFalse();
        using var selected = new BehaviorSubject<IValidationContext?>(context);
        await Assert.That(() => selected.BindObservableValidationState(
            static value => value.ValidationStatusChange,
            (Func<IValidationState, bool>)null!,
            static _ => { })).Throws<ArgumentNullException>();
        await Assert.That(() => selected.BindObservablePropertyValidationState(static value => value, "Name", static values => values.Count, (Action<int>)null!, true)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies admission rollback removes a partly added rule and releases its activated upstream connection.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task RejectedRegistrationRollsBackMembershipAndSubscription()
    {
        using var context = new RejectingContext();
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        var failure = await Assert.That(() => ((IValidationContext)context).AddObservableRule(states, ["Name"])).Throws<InvalidOperationException>();
        await Assert.That(failure).IsSameReferenceAs(context.AdmissionError);
        await Assert.That(context.Activated).IsTrue();
        await Assert.That(context.Validations.Count).IsEqualTo(0);
        await Assert.That(states.HasObservers).IsFalse();
        states.OnNext(new ValidationState(false, "still usable"));
    }

    /// <summary>Verifies rollback failure retains the admission error and still disposes the active source.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task RollbackFailureRetainsOriginalAdmissionError()
    {
        using var context = new RejectingContext { FailRollback = true };
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        var failure = await Assert.That(() => ((IValidationContext)context).AddObservableRule(states, ["Name"])).Throws<AggregateException>();
        await Assert.That(failure!.InnerExceptions[0]).IsSameReferenceAs(context.AdmissionError);
        await Assert.That(failure.InnerExceptions[1]).IsSameReferenceAs(context.RollbackError);
        await Assert.That(context.Validations.Count).IsEqualTo(0);
        await Assert.That(states.HasObservers).IsFalse();
    }

    /// <summary>Verifies failure while the helper activates a supplied stream rolls back already added membership.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SourceActivationFailureRollsBackRegistration()
    {
        using var context = new ValidationContext();
        var source = new FailingSource();
        var failure = await Assert.That(() => context.AddObservableRule(source, ["Name"])).Throws<InvalidOperationException>();
        await Assert.That(failure).IsSameReferenceAs(source.SubscriptionError);
        await Assert.That(context.Validations.Count).IsEqualTo(0);
    }

    /// <summary>Verifies first-seed callbacks can replace membership and reselect the same context without missed changes.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitialSeedReentrancyRecoversLatestMembershipAndSelection()
    {
        using var context = new ValidationContext();
        using var oldStates = new BehaviorSubject<IValidationState>(new ValidationState(false, "old"));
        using var newStates = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var oldRule = context.AddObservableRule(oldStates, ["Name"]);
        using var sources = new BehaviorSubject<IValidationContext?>(context);
        var values = new List<bool>();
        ValidationHelper? replacement = null;
        var changed = false;
        using var binding = sources.BindObservablePropertyValidationState(
            static selected => selected,
            "Name",
            static states => states.All(static state => state.IsValid),
            value =>
            {
                values.Add(value);
                if (!changed)
                {
                    changed = true;
                    context.Remove(context.Validations.Items[0]);
                    replacement = context.AddObservableRule(newStates, ["Name"]);
                    sources.OnNext(null);
                    sources.OnNext(context);
                }
            },
            true);
        try
        {
            await Assert.That(values[0]).IsFalse();
            await Assert.That(values[^1]).IsTrue();
            var count = values.Count;
            oldStates.OnNext(ValidationState.Valid);
            await Assert.That(values.Count).IsEqualTo(count);
            newStates.OnNext(new ValidationState(false, "latest"));
            await Assert.That(values[^1]).IsFalse();
        }
        finally
        {
            replacement?.Dispose();
        }
    }

    /// <summary>A non-reactive owner proves registration and selection use only supplied inputs.</summary>
    private sealed class PlainModel : IValidatableViewModel
    {
        /// <inheritdoc/>
        public IValidationContext ValidationContext { get; set; } = null!;
    }

    /// <summary>A supplied observable whose subscription cannot be established.</summary>
    private sealed class FailingSource : IObservable<IValidationState>
    {
        /// <summary>Gets the original activation failure.</summary>
        internal InvalidOperationException SubscriptionError { get; } = new("Supplied source activation failed.");

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The source cannot establish a subscription.</exception>
        public IDisposable Subscribe(IObserver<IValidationState> observer) => throw SubscriptionError;
    }

    /// <summary>A custom context that accepts and activates a component before rejecting admission.</summary>
    private sealed class RejectingContext : ValidationContext, IValidationContext
    {
        /// <summary>Gets the original admission failure.</summary>
        internal InvalidOperationException AdmissionError { get; } = new("Admission rejected after activation.");

        /// <summary>Gets the rollback failure.</summary>
        internal InvalidOperationException RollbackError { get; } = new("Rollback rejected after removal.");

        /// <summary>Gets a value indicating whether rollback rejects removal.</summary>
        internal bool FailRollback { get; init; }

        /// <summary>Gets a value indicating whether admission connected the state source.</summary>
        internal bool Activated { get; private set; }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">Admission fails after adding and activating the supplied rule.</exception>
        public new void Add(IValidationComponent validation)
        {
            base.Add(validation);
            Activated = validation.IsValid;
            throw AdmissionError;
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">Rollback fails after removing the supplied rule.</exception>
        public new void Remove(IValidationComponent validation)
        {
            base.Remove(validation);
            if (FailRollback)
            {
                throw RollbackError;
            }
        }
    }
}
