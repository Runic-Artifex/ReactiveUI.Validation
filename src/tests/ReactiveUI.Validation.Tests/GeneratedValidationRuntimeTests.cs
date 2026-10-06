// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Regressions for statically selected generated observation and target lifetime infrastructure.</summary>
public class GeneratedValidationRuntimeTests
{
    /// <summary>The InitialValue fixture value.</summary>
    private const string InitialValue = "initial";

    /// <summary>The FirstValue fixture value.</summary>
    private const string FirstValue = "first";

    /// <summary>The SecondValue fixture value.</summary>
    private const string SecondValue = "second";

    /// <summary>The OldValue fixture value.</summary>
    private const string OldValue = "old";

    /// <summary>The CurrentValue fixture value.</summary>
    private const string CurrentValue = "current";

    /// <summary>The UpdatedValue fixture value.</summary>
    private const string UpdatedValue = "updated";

    /// <summary>The AssignmentFailure fixture value.</summary>
    private const string AssignmentFailure = "assignment";

    /// <summary>The number of deliveries for initial state and one replacement.</summary>
    private const int InitialAndReplacementCount = 2;

    /// <summary>The ExpectedValues1 notification sequence.</summary>
    private static readonly string?[] ExpectedValues1 = [null];

    /// <summary>The ExpectedValues2 notification sequence.</summary>
    private static readonly string?[] ExpectedValues2 = [null, OldValue, CurrentValue, UpdatedValue, null];

    /// <summary>The ExpectedValues3 notification sequence.</summary>
    private static readonly string?[] ExpectedValues3 = [FirstValue, SecondValue];

    /// <summary>The ExpectedValues4 notification sequence.</summary>
    private static readonly string?[] ExpectedValues4 = [InitialValue, "next"];

    /// <summary>The ExpectedValues5 notification sequence.</summary>
    private static readonly string?[] ExpectedValues5 = [InitialValue, null];

    /// <summary>The ExpectedValues6 notification sequence.</summary>
    private static readonly string?[] ExpectedValues6 = [InitialValue];

    /// <summary>The ExpectedValues7 notification sequence.</summary>
    private static readonly string?[] ExpectedValues7 = [OldValue, CurrentValue];

    /// <summary>Observes an initially null path and detaches replaced intermediate owners.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task NestedPathEmitsInitialDefaultAndFollowsReplacement()
    {
        var root = new NotificationNode();
        var old = new NotificationNode { Value = OldValue };
        var current = new NotificationNode { Value = CurrentValue };
        var values = new List<string?>();
        var subscription = GeneratedValidationObservation.Observe(
            () => root.Child?.Value,
            new GeneratedValidationProperty(() => root, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(() => root.Child, nameof(NotificationNode.Value))).Subscribe(values.Add);
        await Assert.That(values).IsEquivalentTo(ExpectedValues1);
        root.Child = old;
        root.Child = current;
        await Assert.That(old.ListenerCount).IsEqualTo(0);
        old.Value = "stale";
        current.Value = UpdatedValue;
        root.Child = null;
        await Assert.That(values).IsEquivalentTo(ExpectedValues2);
        subscription.Dispose();
        subscription.Dispose();
        await Assert.That(root.ListenerCount).IsEqualTo(0);
        await Assert.That(current.ListenerCount).IsEqualTo(0);
    }

    /// <summary>Initial notification reentry attaches only the final owner and retains the latest value.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitialCallbackReplacementIsDrainedWithoutStaleHandlers()
    {
        var first = new NotificationNode { Value = FirstValue };
        var second = new NotificationNode { Value = SecondValue };
        var root = new NotificationNode { Child = first };
        var values = new List<string?>();
        using var subscription = GeneratedValidationObservation.Observe(
            () => root.Child?.Value,
            new GeneratedValidationProperty(() => root, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(() => root.Child, nameof(NotificationNode.Value))).Subscribe(value =>
            {
                values.Add(value);
                if (value == FirstValue)
                {
                    root.Child = second;
                }
            });
        await Assert.That(values).IsEquivalentTo(ExpectedValues3);
        await Assert.That(first.ListenerCount).IsEqualTo(0);
        await Assert.That(second.ListenerCount).IsEqualTo(1);
    }

    /// <summary>Default value equality and broad notification names are handled explicitly.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task WildcardNotificationsRefreshButEqualValuesAreNotRepeated()
    {
        var node = new NotificationNode { Value = InitialValue };
        var values = new List<string?>();
        using var subscription = GeneratedValidationObservation.Observe(
            () => node.Value,
            new GeneratedValidationProperty(() => node, nameof(NotificationNode.Value))).Subscribe(values.Add);
        node.SetWithoutNotification("next");
        node.Raise(null);
        node.Raise(string.Empty);
        node.Raise("Unrelated");
        await Assert.That(values).IsEquivalentTo(ExpectedValues4);
    }

    /// <summary>A throwing getter or initial callback releases every attached property handler.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitialFailureDetachesObservation()
    {
        var leaf = new NotificationNode();
        var node = new NotificationNode { Child = leaf };
        var getterError = new InvalidOperationException("getter");
        var callbackError = new InvalidOperationException("callback");
        var getter = GeneratedValidationObservation.Observe<string?>(
            () => throw getterError,
            new GeneratedValidationProperty(() => node, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(() => node.Child, nameof(NotificationNode.Value)));
        var callback = GeneratedValidationObservation.Observe(
            () => node.Child?.Value,
            new GeneratedValidationProperty(() => node, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(() => node.Child, nameof(NotificationNode.Value)));
        await Assert.That(() => getter.Subscribe(static _ => { })).Throws<InvalidOperationException>();
        await Assert.That(node.ListenerCount).IsEqualTo(0);
        await Assert.That(leaf.ListenerCount).IsEqualTo(0);
        await Assert.That(() => callback.Subscribe(_ => throw callbackError)).Throws<InvalidOperationException>();
        await Assert.That(node.ListenerCount).IsEqualTo(0);
        await Assert.That(leaf.ListenerCount).IsEqualTo(0);
    }

    /// <summary>Reference selections follow replacements even when objects override equality.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [SuppressMessage("Usage", "SST2496:Repeated disposal", Justification = "This regression deliberately verifies repeated disposal is idempotent.")]
    public async Task EqualReferenceReplacementReplaysLatestTargetValue()
    {
        var first = new EqualTarget();
        var second = new EqualTarget();
        var root = new TargetOwner { Target = first };
        using var states = new BehaviorSubject<string>(InitialValue);
        var targets = GeneratedValidationObservation.ObserveReference(
            () => root.Target,
            new GeneratedValidationProperty(() => root, nameof(TargetOwner.Target)));
        using var binding = GeneratedValidationBindingSupport.BindToTarget(
            targets,
            callback => new CallbackBinding(states.Subscribe(callback)),
            static (EqualTarget target, string value) => target.Value = value);
        root.Target = second;
        await Assert.That(second.Value).IsEqualTo(InitialValue);
        states.OnNext(UpdatedValue);
        await Assert.That(second.Value).IsEqualTo(UpdatedValue);
        await Assert.That(first.Value).IsEqualTo(InitialValue);
        root.Target = null;
        states.OnNext("while-null");
        root.Target = first;
        await Assert.That(first.Value).IsEqualTo("while-null");
        binding.Dispose();
        binding.Dispose();
        await Assert.That(root.ListenerCount).IsEqualTo(0);
        await Assert.That(states.HasObservers).IsFalse();
    }

    /// <summary>Initial assignment can replace both the parent and validation value without stale writes.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ReentrantTargetAndValueReplacementDrainTheCurrentPair()
    {
        var first = new EqualTarget();
        var second = new EqualTarget();
        using var targets = new BehaviorSubject<EqualTarget?>(first);
        using var values = new BehaviorSubject<string>(FirstValue);
        using var binding = GeneratedValidationBindingSupport.BindToTarget(
            targets,
            callback => new CallbackBinding(values.Subscribe(callback)),
            (EqualTarget target, string value) =>
            {
                target.Value = value;
                if (ReferenceEquals(target, first))
                {
                    targets.OnNext(second);
                    values.OnNext(SecondValue);
                }
            });
        await Assert.That(second.Value).IsEqualTo(SecondValue);
        await Assert.That(first.Value).IsEqualTo(FirstValue);
    }

    /// <summary>Assignment failure releases the already owned parent and synchronously subscribing validation source.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitialSetterFailureReleasesBothSubscriptions()
    {
        var owner = new TargetOwner { Target = new() };
        var node = new NotificationNode { Value = InitialValue };
        var targets = GeneratedValidationObservation.ObserveReference(
            () => owner.Target,
            new GeneratedValidationProperty(() => owner, nameof(TargetOwner.Target)));
        var values = GeneratedValidationObservation.Observe(
            () => node.Value,
            new GeneratedValidationProperty(() => node, nameof(NotificationNode.Value)));
        await Assert.That(() => GeneratedValidationBindingSupport.BindToTarget(
            targets,
            callback => new CallbackBinding(values.Subscribe(callback)),
            static (EqualTarget _, string? _) => throw new InvalidOperationException(AssignmentFailure))).Throws<InvalidOperationException>();
        await Assert.That(owner.ListenerCount).IsEqualTo(0);
        await Assert.That(node.ListenerCount).IsEqualTo(0);
    }

    /// <summary>A callback may dispose the binding and replace a target without retaining either source subscription.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ReentrantDisposalDetachesBothSources()
    {
        using var targets = new BehaviorSubject<EqualTarget?>(new EqualTarget());
        using var values = new BehaviorSubject<string>(InitialValue);
        IValidationBinding? binding = null;
        binding = GeneratedValidationBindingSupport.BindToTarget(
            targets,
            callback => new CallbackBinding(values.Subscribe(callback)),
            (EqualTarget target, string value) =>
            {
                target.Value = value;
                if (value == "dispose")
                {
                    binding!.Dispose();
                    targets.OnNext(new());
                }
            });
        values.OnNext("dispose");
        await Assert.That(targets.HasObservers).IsFalse();
        await Assert.That(values.HasObservers).IsFalse();
        binding.Dispose();
    }

    /// <summary>Initial callback null replacement emits default and removes the previously current leaf.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitialCallbackNullReplacementDetachesLeaf()
    {
        var leaf = new NotificationNode { Value = InitialValue };
        var root = new NotificationNode { Child = leaf };
        var values = new List<string?>();
        using var subscription = GeneratedValidationObservation.Observe(
            () => root.Child?.Value,
            new GeneratedValidationProperty(() => root, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(() => root.Child, nameof(NotificationNode.Value))).Subscribe(value =>
            {
                values.Add(value);
                root.Child = null;
            });
        await Assert.That(values).IsEquivalentTo(ExpectedValues5);
        await Assert.That(leaf.ListenerCount).IsEqualTo(0);
        leaf.Value = "stale";
        await Assert.That(values.Count).IsEqualTo(InitialAndReplacementCount);
    }

    /// <summary>An earlier multicast subscriber can dispose the later observation registration safely.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task EarlierEventCallbackDisposesLaterObservation()
    {
        var node = new NotificationNode { Value = InitialValue };
        IDisposable? subscription = null;
        PropertyChangedEventHandler dispose = (_, _) => subscription!.Dispose();
        node.PropertyChanged += dispose;
        var values = new List<string?>();
        subscription = GeneratedValidationObservation.Observe(
            () => node.Value,
            new GeneratedValidationProperty(() => node, nameof(NotificationNode.Value))).Subscribe(values.Add);
        node.Value = "after-disposal";
        await Assert.That(values).IsEquivalentTo(ExpectedValues6);
        await Assert.That(node.ListenerCount).IsEqualTo(1);
        node.PropertyChanged -= dispose;
        subscription.Dispose();
    }

    /// <summary>A binding disposed by a synchronous initial failure releases a factory token returned afterward.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitialFailureDisposesLateFactoryToken()
    {
        using var targets = new BehaviorSubject<EqualTarget?>(new EqualTarget());
        var tokenDisposed = new StrongBox<bool>();
        using var binding = GeneratedValidationBindingSupport.BindToTarget(
            targets,
            callback =>
            {
                try
                {
                    callback(InitialValue);
                }
                catch (InvalidOperationException)
                {
                    // Deliberately return a late token after synchronous disposal to verify ownership.
                }

                return new CallbackBinding(System.Reactive.Disposables.Disposable.Create(tokenDisposed, static box => box.Value = true));
            },
            static (EqualTarget _, string _) => throw new InvalidOperationException(AssignmentFailure));
        await Assert.That(tokenDisposed.Value).IsTrue();
        await Assert.That(targets.HasObservers).IsFalse();
    }

    /// <summary>A detached registration already in an event multicast snapshot cannot refresh the replacement path.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ObsoleteMulticastHandlerDoesNotEvaluateCurrentGetter()
    {
        var old = new NotificationNode { Value = OldValue };
        var current = new NotificationNode { Value = CurrentValue };
        var root = new NotificationNode { Child = old };
        var getterCount = 0;
        PropertyChangedEventHandler replace = (_, _) => root.Child = current;
        old.PropertyChanged += replace;
        var values = new List<string?>();
        using var subscription = GeneratedValidationObservation.Observe(
            () =>
            {
                getterCount++;
                return root.Child?.Value;
            },
            new GeneratedValidationProperty(() => root, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(() => root.Child, nameof(NotificationNode.Value))).Subscribe(values.Add);
        old.Value = "stale-notification";
        await Assert.That(getterCount).IsEqualTo(InitialAndReplacementCount);
        await Assert.That(values).IsEquivalentTo(ExpectedValues7);
        old.PropertyChanged -= replace;
        await Assert.That(old.ListenerCount).IsEqualTo(0);
    }

    /// <summary>A nested owner getter can dispose during refresh without attaching a handler after disposal.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ReentrantOwnerGetterDisposalDoesNotAttachReturnedOwner()
    {
        var old = new NotificationNode { Value = OldValue };
        var current = new NotificationNode { Value = CurrentValue };
        var root = new NotificationNode { Child = old };
        IDisposable? subscription = null;
        var disposeDuringOwner = false;
        var values = new List<string?>();
        subscription = GeneratedValidationObservation.Observe(
            () => root.Child?.Value,
            new GeneratedValidationProperty(() => root, nameof(NotificationNode.Child)),
            new GeneratedValidationProperty(
                () =>
                {
                    if (disposeDuringOwner)
                    {
                        subscription!.Dispose();
                    }

                    return root.Child;
                },
                nameof(NotificationNode.Value))).Subscribe(values.Add);
        disposeDuringOwner = true;
        root.Child = current;
        await Assert.That(root.ListenerCount).IsEqualTo(0);
        await Assert.That(old.ListenerCount).IsEqualTo(0);
        await Assert.That(current.ListenerCount).IsEqualTo(0);
        await Assert.That(values.Count).IsEqualTo(1);
        await Assert.That(values[0]).IsEqualTo(OldValue);
        subscription.Dispose();
    }

    /// <summary>Static observable API calls accept nullable values and nullable reference or struct projections.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [SuppressMessage("Style", "SST2251:Remove explicit type arguments", Justification = "This regression exercises the exact static nullable generic signatures emitted by the generator.")]
    public async Task StaticObservableApisAcceptNullableValuesAndProjections()
    {
        using var context = new ValidationContext();
        using var values = new BehaviorSubject<string?>(null);
        using var sources = new BehaviorSubject<IValidationContext?>(context);
        using var rule = ObservableValidationRuleExtensions.AddObservableRule<string?>(
            context,
            values,
            static value => new ValidationState(value is not null, InitialValue),
            [InitialValue]);
        var referenceOutputs = new List<string?>();
        var structOutputs = new List<int?>();
        using var whole = ObservableValidationBindingExtensions.BindObservableValidationState<IValidationContext, string?>(
            sources,
            static selected => selected.ValidationStatusChange,
            static _ => null,
            referenceOutputs.Add);
        using var property = ObservableValidationBindingExtensions.BindObservablePropertyValidationState<IValidationContext, int?>(
            sources,
            static selected => selected,
            InitialValue,
            static _ => null,
            structOutputs.Add,
            true);
        values.OnNext(UpdatedValue);
        await Assert.That(referenceOutputs.Count > 0).IsTrue();
        await Assert.That(structOutputs.Count > 0).IsTrue();
        await Assert.That(referenceOutputs.TrueForAll(static value => value is null)).IsTrue();
        await Assert.That(structOutputs.TrueForAll(static value => value is null)).IsTrue();
    }

    /// <summary>Owns a supplied callback subscription.</summary>
    /// <param name="subscription">The callback subscription to own.</param>
    private sealed class CallbackBinding(IDisposable subscription) : IValidationBinding
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => subscription.Dispose();
    }

    /// <summary>A distinct target whose equality deliberately ignores object identity.</summary>
    private sealed class EqualTarget
    {
        /// <summary>Gets or sets the fixture value.</summary>
        public string? Value { get; set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is EqualTarget;

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>A manually notifying parent for target replacement.</summary>
    private sealed class TargetOwner : INotifyPropertyChanged
    {
        /// <summary>The current target owner notification handlers.</summary>
        private PropertyChangedEventHandler? _changed;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        /// <summary>Gets the number of currently attached notification handlers.</summary>
        public int ListenerCount => _changed?.GetInvocationList().Length ?? 0;

        /// <summary>Gets or sets the current target and raises its notification.</summary>
        public EqualTarget? Target
        {
            get => field;
            set
            {
                field = value;
                _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Target)));
            }
        }
    }

    /// <summary>A manually notifying nested property owner.</summary>
    private sealed class NotificationNode : INotifyPropertyChanged
    {
        /// <summary>The current nested node notification handlers.</summary>
        private PropertyChangedEventHandler? _changed;

        /// <summary>The current value fixture state.</summary>
        private string? _value;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        /// <summary>Gets the number of currently attached notification handlers.</summary>
        public int ListenerCount => _changed?.GetInvocationList().Length ?? 0;

        /// <summary>Gets or sets the nested owner and raises its notification.</summary>
        public NotificationNode? Child
        {
            get => field;
            set
            {
                field = value;
                Raise(nameof(Child));
            }
        }

        /// <summary>Gets or sets the fixture value.</summary>
        public string? Value
        {
            get => _value;
            set
            {
                _value = value;
                Raise(nameof(Value));
            }
        }

        /// <summary>Raises an exact or wildcard property notification.</summary>
        /// <param name="name">The property notification name.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Raise(string? name) => _changed?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Changes the selected value without raising a notification.</summary>
        /// <param name="value">The selected fixture value.</param>
        public void SetWithoutNotification(string? value) => _value = value;
    }
}
