// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Collections;
using ReactiveUI.Validation.Reactive.Components.Abstractions;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Formatters;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Validation.Reactive.States;
#else
using ReactiveUI.Validation.Collections;
using ReactiveUI.Validation.Components.Abstractions;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Formatters;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Validation.States;
#endif

/// <summary>Runs the virtual error-hook contract in the shared actual-package application.</summary>
/// <remarks>Uses the application's Current and Observer helpers; the payload adds no package or publish stage.</remarks>
internal static class ErrorHookScenario
{
    private const string NestedPath = "Editor.Name";
    private const string RequiredMessage = "required";
    private const string DetailMessage = "preserved detail";
    private const string CombinedMessage = RequiredMessage + " " + DetailMessage;
    private const string RichCode = "R09";
    private const int RichRevision = 7;
    private const int InitialNotifications = 4;
    private const int TextItems = 2;

    /// <summary>Executes forwarding, ordering, rich-state, metadata, reentry and borrowed-lifetime checks.</summary>
    internal static void Run()
    {
        ForwardingAndBaseDispatch();
        StateOrderingAndBorrowedOwnership();
        MetadataRenameAndReentrantRemoval();
        PropertyChangeMayDisposeBeforeTheHook();
    }

    private static void ForwardingAndBaseDispatch()
    {
        using var model = new HookModel();
        var events = new List<string?>();
        model.ErrorsChanged += (_, args) => events.Add(args.PropertyName);
        model.NotifyAll();
        model.Notify(string.Empty);
        model.Notify(null);
        model.Notify(NestedPath);
        CheckPaths(model.Paths, [string.Empty, string.Empty, null, NestedPath]);
        CheckPaths(events, model.Paths);

        model.Forward = false;
        model.Notify("Name");
        Check(model.Paths[^1] == "Name" && events.Count == InitialNotifications,
            "A derived hook controls event delivery by choosing whether to call the base implementation.");
    }

    private static void StateOrderingAndBorrowedOwnership()
    {
        var model = new HookModel();
        var rich = new RichState(ValidationText.Create(RequiredMessage, DetailMessage));
        var states = new Current<IValidationState>(rich);
        var atEvent = new List<bool>();
        model.ErrorsChanged += (_, _) => atEvent.Add(model.HasErrors);
        ValidationHelper? rule = null;
        IDisposable? rawStates = null;
        try
        {
            rule = model.AddObservableRule(states, [NestedPath]);
            IValidationState? observed = null;
            rawStates = rule.ValidationChanged.Subscribe(new Observer<IValidationState>(state => observed = state));
            Check(ReferenceEquals(observed, rich) && observed is RichState { Code: RichCode, Revision: RichRevision },
                "The rule's raw state stream preserves the original rich validation state and its custom metadata.");
            Check(rule.Message.Count == TextItems && rule.Message[0] == RequiredMessage && rule.Message[1] == DetailMessage,
                "The helper preserves every validation text item.");
            Check(model.HasErrors && model.AtHook[^1] && atEvent[^1],
                "HasErrors is current before both the virtual hook and ErrorsChanged callback.");
            Check(ContainsError(model.GetErrors(NestedPath), CombinedMessage)
                && ContainsError(model.GetErrors(null), CombinedMessage)
                && ContainsError(model.GetErrors(string.Empty), CombinedMessage)
                && !HasErrors(model.GetErrors("Editor")),
                "Nested and entity queries preserve complete paths and every formatted text item.");

            states.Set(ValidationState.Valid);
            Check(!model.HasErrors && !model.AtHook[^1] && !atEvent[^1] && !HasErrors(model.GetErrors(null)),
                "Valid state is visible before the hook and event, and clears entity errors.");
            model.Dispose();
            var hooks = model.Paths.Count;
            var events = atEvent.Count;
            states.Set(rich);
            Check(model.Paths.Count == hooks && atEvent.Count == events && states.Subscribers > 0 && !rule.IsValid,
                "Model disposal stops its callbacks while leaving caller-owned helpers and sources usable.");
        }
        finally
        {
            rawStates?.Dispose();
            rule?.Dispose();
            model.Dispose();
        }

        Check(states.Subscribers == 0, "Ending the explicitly owned helper releases its source subscription.");
    }

    private static void MetadataRenameAndReentrantRemoval()
    {
        using var model = new HookModel();
        var rich = new RichState(ValidationText.Create(RequiredMessage, DetailMessage));
        var states = new Current<IValidationState>(rich);
        var component = new MutablePaths(states) { Paths = ["A"] };
        model.ValidationContext.Add(component);
        model.Paths.Clear();
        component.Paths = ["B", "B"];
        states.Set(rich);
        CheckPaths(model.Paths, ["A", "B"]);
        Check(!HasErrors(model.GetErrors("A")) && ContainsError(model.GetErrors("B"), CombinedMessage)
            && model.AtHook[^1] && ReferenceEquals(states.Value, rich),
            "Unchanged rich state can rename display metadata, clearing the old path once and notifying the new path once.");

        var removed = false;
        model.ErrorsChanged += (_, _) =>
        {
            if (!removed)
            {
                removed = true;
                model.ValidationContext.Remove(component);
            }
        };
        component.Paths = ["C"];
        states.Set(rich);
        Check(removed && !HasComponents(model.ValidationContext.Validations.Items)
            && !HasErrors(model.GetErrors(null)) && !model.HasErrors && states.Subscribers == 0 && !component.Disposed,
            "Reentrant removal ends owned membership subscriptions without disposing the borrowed component or source.");
        var hooks = model.Paths.Count;
        states.Set(ValidationState.Valid);
        Check(model.Paths.Count == hooks && states.Value.IsValid, "A removed borrowed source stays usable and cannot notify the model.");
    }

    private static void PropertyChangeMayDisposeBeforeTheHook()
    {
        var model = new HookModel();
        var states = new Current<IValidationState>(ValidationState.Valid);
        ValidationHelper? rule = null;
        try
        {
            rule = model.AddObservableRule(states, [NestedPath]);
            model.Paths.Clear();
            var events = 0;
            model.ErrorsChanged += (_, _) => events++;
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(model.HasErrors))
                {
                    model.Dispose();
                }
            };
            states.Set(new RichState(ValidationText.Create(RequiredMessage, DetailMessage)));
            Check(model.Paths.Count == 0 && EqualityComparer<int>.Default.Equals(events, 0) && states.Subscribers > 0,
                "Disposal during HasErrors PropertyChanged prevents later virtual hooks while preserving borrowed helper ownership.");
        }
        finally
        {
            rule?.Dispose();
            model.Dispose();
        }

        Check(states.Subscribers == 0, "The caller can release the helper after reentrant model disposal.");
    }

    private static void CheckPaths(List<string?> actual, List<string?> expected)
    {
        Check(actual.Count == expected.Count, "The virtual hook preserves the exact notification count.");
        for (var index = 0; index < expected.Count; index++)
        {
            Check(StringComparer.Ordinal.Equals(actual[index], expected[index]), "The virtual hook preserves ordered empty, null and complete paths.");
        }
    }

    private static bool ContainsError(IEnumerable errors, string expected)
    {
        foreach (var error in errors)
        {
            if (error is string text && StringComparer.Ordinal.Equals(text, expected))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasErrors(IEnumerable errors)
    {
        var enumerator = errors.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private static bool HasComponents(IEnumerable<IValidationComponent> components)
    {
        using var enumerator = components.GetEnumerator();
        return enumerator.MoveNext();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RichState(IValidationText text) : IValidationState
    {
        public bool IsValid => false;
        public IValidationText Text { get; } = text;
        internal string Code { get; } = RichCode;
        internal int Revision { get; } = RichRevision;
    }

    private sealed class MutablePaths(Current<IValidationState> states) : IPropertyValidationComponent, IDisposable
    {
        public bool IsValid => states.Value.IsValid;
        public IValidationText Text => states.Value.Text;
        public IObservable<IValidationState> ValidationStatusChange => states;
        public int PropertyCount => Paths.Length;
        public IEnumerable<string> Properties => Paths;
        internal string[] Paths { get; set; } = [];
        internal bool Disposed { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Disposed = true;

        public bool ContainsPropertyName(string propertyName, bool exclusively = false)
        {
            var found = false;
            string? unique = null;
            foreach (var path in Paths)
            {
                if (unique is not null && !StringComparer.Ordinal.Equals(unique, path) && exclusively)
                {
                    return false;
                }

                unique = path;
                found |= StringComparer.Ordinal.Equals(path, propertyName);
            }

            return found;
        }
    }

    private sealed class HookModel : ReactiveValidationObject
    {
        internal HookModel()
            : base(null, SingleLineFormatter.Default)
        {
        }

        internal List<string?> Paths { get; } = [];
        internal List<bool> AtHook { get; } = [];
        internal bool Forward { get; set; } = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyAll() => RaiseErrorsChanged();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Notify(string? path) => RaiseErrorsChanged(path!);

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
