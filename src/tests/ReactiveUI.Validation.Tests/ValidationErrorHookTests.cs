// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Protects derived error delivery and the base notification contract.</summary>
[SuppressMessage("Usage", "SST2496:Repeated disposal", Justification = "These ownership regressions deliberately exercise early, repeated and reentrant disposal.")]
public class ValidationErrorHookTests
{
    /// <summary>The NestedPath test value.</summary>
    private const string NestedPath = "Editor.Name";

    /// <summary>The RequiredMessage test value.</summary>
    private const string RequiredMessage = "required";

    /// <summary>The ordered old and new metadata names.</summary>
    private static readonly string?[] OldAndNewPaths = ["A", "B"];

    /// <summary>Checks the whole-object overload, empty, null and complete paths reach the override.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ForwardingPreservesPropertyNamesAndInvokesDerivedHook()
    {
        using var model = new HookModel();
        var events = new List<string?>();
        model.ErrorsChanged += (_, args) => events.Add(args.PropertyName);
        model.NotifyAll();
        model.Notify(string.Empty);
        model.Notify(null);
        model.Notify(NestedPath);
        await Assert.That(model.Paths).IsEquivalentTo(new string?[] { string.Empty, string.Empty, null, NestedPath });
        await Assert.That(events).IsEquivalentTo(model.Paths);
    }

    /// <summary>Checks that an override deliberately withholding the base call controls event delivery.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SubscribersAreNotNotifiedWithoutTheBaseCall()
    {
        using var model = new HookModel { Forward = false };
        var count = 0;
        model.ErrorsChanged += (_, _) => count++;
        model.Notify("Name");
        await Assert.That(model.Paths).Contains("Name");
        await Assert.That(count).IsEqualTo(0);
    }

    /// <summary>Checks synchronous current validity is visible in overrides and events before disposal detaches rules.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task HasErrorsPrecedesHookAndSubscriptionDisposalStopsDelivery()
    {
        var model = new HookModel();
        using var states = new BehaviorSubject<IValidationState>(new ValidationState(false, RequiredMessage));
        var atEvent = new List<bool>();
        model.ErrorsChanged += (_, _) => atEvent.Add(model.HasErrors);
        using var rule = model.AddObservableRule(states, [NestedPath]);
        await Assert.That(model.AtHook[^1]).IsTrue();
        await Assert.That(atEvent[^1]).IsTrue();
        states.OnNext(ValidationState.Valid);
        await Assert.That(model.AtHook[^1]).IsFalse();
        await Assert.That(atEvent[^1]).IsFalse();
        model.Dispose();
        model.Dispose();
        var count = model.Paths.Count;
        states.OnNext(new ValidationState(false, "after disposal"));
        await Assert.That(model.Paths.Count).IsEqualTo(count);
        await Assert.That(states.HasObservers).IsTrue();
        rule.Dispose();
        await Assert.That(states.HasObservers).IsFalse();
    }

    /// <summary>Checks replaced display metadata clears the old path even when validity and text are identical.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ChangedMetadataNotifiesOldAndNewPathsOnceAndAllowsReentrantRemoval()
    {
        using var model = new HookModel();
        using var states = new BehaviorSubject<IValidationState>(new ValidationState(false, RequiredMessage));
        var component = new MutablePaths(states) { Paths = ["A"] };
        model.ValidationContext.Add(component);
        model.Paths.Clear();
        component.Paths = ["B", "B"];
        states.OnNext(states.Value);
        await Assert.That(model.Paths).IsEquivalentTo(OldAndNewPaths);
        await Assert.That(model.GetErrors("A").Cast<string>()).IsEmpty();
        await Assert.That(model.GetErrors("B").Cast<string>()).Contains(RequiredMessage);
        await Assert.That(model.AtHook[^1]).IsTrue();
        model.ErrorsChanged += (_, _) => model.ValidationContext.Remove(component);
        component.Paths = ["C"];
        states.OnNext(states.Value);
        await Assert.That(model.ValidationContext.Validations.Items).IsEmpty();
        await Assert.That(model.GetErrors(null).Cast<string>()).IsEmpty();
    }

    /// <summary>Checks disposal during the HasErrors property notification prevents later virtual error delivery.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task HasErrorsPropertyNotificationMayDisposeBeforeErrorHook()
    {
        using var model = new HookModel();
        using var states = new BehaviorSubject<IValidationState>(ValidationState.Valid);
        using var rule = model.AddObservableRule(states, ["Name"]);
        model.Paths.Clear();
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(model.HasErrors))
            {
                model.Dispose();
            }
        };
        states.OnNext(new ValidationState(false, RequiredMessage));
        await Assert.That(model.Paths).IsEmpty();
        rule.Dispose();
        await Assert.That(states.HasObservers).IsFalse();
    }

    /// <summary>A component whose complete display metadata can change independently of its state.</summary>
    /// <param name="states">The unchanged-state notification source.</param>
    private sealed class MutablePaths(BehaviorSubject<IValidationState> states) : IPropertyValidationComponent
    {
        /// <inheritdoc/>
        public bool IsValid => states.Value.IsValid;

        /// <inheritdoc/>
        public IValidationText Text => states.Value.Text!;

        /// <inheritdoc/>
        public IObservable<IValidationState> ValidationStatusChange => states;

        /// <inheritdoc/>
        public int PropertyCount => Paths.Length;

        /// <inheritdoc/>
        public IEnumerable<string> Properties => Paths;

        /// <summary>Gets or sets the current display names.</summary>
        internal string[] Paths { get; set; } = [];

        /// <inheritdoc/>
        public bool ContainsPropertyName(string propertyName, bool exclusively = false) =>
            Paths.Contains(propertyName, StringComparer.Ordinal) && (!exclusively || Paths.Distinct(StringComparer.Ordinal).Count() == 1);
    }

    /// <summary>A derived object that records the virtual notification contract.</summary>
    private sealed class HookModel : ReactiveValidationObject
    {
        /// <summary>Gets the names delivered to the override.</summary>
        internal List<string?> Paths { get; } = [];

        /// <summary>Gets current validity as seen in the override.</summary>
        internal List<bool> AtHook { get; } = [];

        /// <summary>Gets or sets whether the override forwards to event subscribers.</summary>
        internal bool Forward { get; set; } = true;

        /// <summary>Invokes the parameterless whole-object notification.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyAll() => RaiseErrorsChanged();

        /// <summary>Invokes a named notification, preserving runtime null compatibility.</summary>
        /// <param name="path">The property name.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Notify(string? path) => RaiseErrorsChanged(path!);

        /// <inheritdoc/>
        protected override void RaiseErrorsChanged(string propertyName)
        {
            Paths.Add(propertyName);
            AtHook.Add(HasErrors);
            if (Forward)
            {
                base.RaiseErrorsChanged(propertyName);
            }
        }
    }
}
