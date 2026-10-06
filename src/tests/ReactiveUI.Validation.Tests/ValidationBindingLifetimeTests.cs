// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using BindingObservable = ReactiveUI.Primitives.Signals.Signal;
using BindingUnit = ReactiveUI.Primitives.RxVoid;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Regressions for binding replacement, null transitions and subscription disposal.</summary>
public class ValidationBindingLifetimeTests
{
    /// <summary>A name accepted by every property rule in this class.</summary>
    private const string ValidName = "valid";

    /// <summary>The first model's validation message.</summary>
    private const string FirstError = "First error";

    /// <summary>The second model's validation message.</summary>
    private const string SecondError = "Second error";

    /// <summary>The public binding overloads and assignment paths exercised by the lifetime tests.</summary>
    public enum BindingPath
    {
        /// <summary>The Property binding path.</summary>
        Property = 0,

        /// <summary>The NestedProperty binding path.</summary>
        NestedProperty = 1,

        /// <summary>The Model binding path.</summary>
        Model = 2,

        /// <summary>The NestedModel binding path.</summary>
        NestedModel = 3,

        /// <summary>The PropertyAction binding path.</summary>
        PropertyAction = 4,

        /// <summary>The NonStrictPropertyAction binding path.</summary>
        NonStrictPropertyAction = 5,

        /// <summary>The ModelAction binding path.</summary>
        ModelAction = 6,

        /// <summary>The Helper binding path.</summary>
        Helper = 7,

        /// <summary>The NestedHelper binding path.</summary>
        NestedHelper = 8,

        /// <summary>The HelperAction binding path.</summary>
        HelperAction = 9,
    }

    /// <summary>All binding paths detach replaced models and clear on null, including reassignment.</summary>
    /// <param name="path">The binding overload and target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Property)]
    [Arguments(BindingPath.NestedProperty)]
    [Arguments(BindingPath.Model)]
    [Arguments(BindingPath.NestedModel)]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.NonStrictPropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.NestedHelper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task ReplacementAndNullDetachPreviousModel(BindingPath path)
    {
        using var first = CreateModel(FirstError);
        using var second = CreateModel(SecondError);
        var view = new TestView(first);
        using var binding = CreateBinding(view, path);
        await Assert.That(ReadMessage(view, path)).IsEqualTo(FirstError);

        view.ViewModel = second;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
        first.Name = ValidName;
        first.Name = string.Empty;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);

        view.ViewModel = null;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        second.Name = ValidName;
        second.Name = string.Empty;
        await Assert.That(ReadMessage(view, path)).IsEmpty();

        view.ViewModel = first;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(FirstError);
        first.Name = ValidName;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        second.Name = ValidName;
        second.Name = string.Empty;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
    }

    /// <summary>A binding can be created before the model is assigned and is cleared initially.</summary>
    /// <param name="path">The binding overload and target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Property)]
    [Arguments(BindingPath.NestedProperty)]
    [Arguments(BindingPath.Model)]
    [Arguments(BindingPath.NestedModel)]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.NonStrictPropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.NestedHelper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task LateAssignmentUpdatesInitiallyEmptyBinding(BindingPath path)
    {
        var view = new TestView { NameErrorLabel = FirstError, NameErrorContainer = { Text = FirstError } };
        using var binding = CreateBinding(view, path);
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        using var model = CreateModel(SecondError);
        view.ViewModel = model;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
    }

    /// <summary>Replacing an invalid model with a model without rules clears the prior message immediately.</summary>
    /// <param name="path">The binding overload and target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Property)]
    [Arguments(BindingPath.NestedProperty)]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.NonStrictPropertyAction)]
    [Arguments(BindingPath.Model)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task ReplacementWithEmptyModelClearsPreviousError(BindingPath path)
    {
        using var first = CreateModel(FirstError);
        using var empty = new TestViewModel();
        var view = new TestView(first);
        using var binding = CreateBinding(view, path);
        await Assert.That(ReadMessage(view, path)).IsEqualTo(FirstError);
        view.ViewModel = empty;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        first.Name = ValidName;
        first.Name = string.Empty;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        using var rule = empty.ValidationRuleUnsafe(vm => vm.Name, static name => !string.IsNullOrEmpty(name), SecondError);
        empty.NameRule = rule;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
    }

    /// <summary>Disposal leaves the last value and stops model assignment and validation updates.</summary>
    /// <param name="path">The binding overload and target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Property)]
    [Arguments(BindingPath.NestedProperty)]
    [Arguments(BindingPath.Model)]
    [Arguments(BindingPath.NestedModel)]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.NonStrictPropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.NestedHelper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task DisposalStopsUpdatesAndIsIdempotent(BindingPath path)
    {
        using var first = CreateModel(FirstError);
        using var second = CreateModel(SecondError);
        var view = new TestView(first);
        var binding = CreateBinding(view, path);
        binding.Dispose();
        await Assert.That(() => binding.Dispose()).ThrowsNothing();
        first.Name = ValidName;
        view.ViewModel = second;
        second.Name = ValidName;
        view.ViewModel = null;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(FirstError);
    }

    /// <summary>Replacing a helper also detaches the previous helper on the same model.</summary>
    /// <param name="path">The helper binding target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.NestedHelper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task HelperReplacementDetachesPreviousHelper(BindingPath path)
    {
        using var model = CreateModel(FirstError);
        using var replacement = model.ValidationRuleUnsafe(vm => vm.Name2, static name => !string.IsNullOrEmpty(name), SecondError);
        var view = new TestView(model);
        using var binding = CreateBinding(view, path);
        model.NameRule = replacement;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
        model.Name = ValidName;
        model.Name = string.Empty;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
        model.NameRule = null;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        model.Name2 = ValidName;
        model.Name2 = string.Empty;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
    }

    /// <summary>Disposal marks the subscription disposed before invoking reentrant cleanup.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ReentrantDisposalRunsCleanupOnce()
    {
        ValidationBinding? binding = null;
        var cleanupCount = 0;
        var source = new CallbackObservable(() =>
        {
            cleanupCount++;
            if (cleanupCount > 1)
            {
                throw new InvalidOperationException("Cleanup was reentered.");
            }

            binding!.Dispose();
        });
        binding = new(source);
        await Assert.That(() => binding.Dispose()).ThrowsNothing();
        await Assert.That(cleanupCount).IsEqualTo(1);
    }

    /// <summary>A completed subscription can still be disposed repeatedly.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task DisposalAfterSynchronousCompletionIsIdempotent()
    {
        var binding = new ValidationBinding(BindingObservable.Empty<BindingUnit>());
        binding.Dispose();
        await Assert.That(() => binding.Dispose()).ThrowsNothing();
    }

    /// <summary>A subscription error remains visible to the caller rather than being swallowed.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SynchronousSubscriptionErrorIsPreserved() =>
        await Assert.That(static () => new ValidationBinding(BindingObservable.Throw<BindingUnit>(new InvalidOperationException(FirstError))))
            .Throws<InvalidOperationException>();

    /// <summary>Detached models and disposed bindings no longer invoke action callbacks.</summary>
    /// <param name="path">The action overload to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.HelperAction)]
    public async Task DetachedAndDisposedActionsDoNotRun(BindingPath path)
    {
        using var first = CreateModel(FirstError);
        using var second = CreateModel(SecondError);
        var view = new TestView(first);
        var calls = 0;
        var binding = CreateActionBinding(view, path, _ => calls++);
        view.ViewModel = second;
        var replacementCalls = calls;
        first.Name = ValidName;
        first.Name = string.Empty;
        await Assert.That(calls).IsEqualTo(replacementCalls);
        view.ViewModel = null;
        var nullCalls = calls;
        second.Name = ValidName;
        second.Name = string.Empty;
        await Assert.That(calls).IsEqualTo(nullCalls);
        binding.Dispose();
        view.ViewModel = first;
        first.Name = ValidName;
        await Assert.That(calls).IsEqualTo(nullCalls);
    }

    /// <summary>An action may synchronously replace its model without retaining the previous stream.</summary>
    /// <param name="path">The action overload to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.HelperAction)]
    public async Task ActionCanReplaceModelDuringNotification(BindingPath path)
    {
        using var first = CreateModel(FirstError);
        using var second = CreateModel(SecondError);
        var view = new TestView(first);
        var replace = false;
        using var binding = CreateActionBinding(view, path, message =>
        {
            view.NameErrorLabel = message;
            if (replace)
            {
                replace = false;
                view.ViewModel = second;
            }
        });
        replace = true;
        first.Name = ValidName;
        await Assert.That(view.NameErrorLabel).IsEqualTo(SecondError);
        first.Name = string.Empty;
        await Assert.That(view.NameErrorLabel).IsEqualTo(SecondError);
        second.Name = ValidName;
        await Assert.That(view.NameErrorLabel).IsEmpty();
    }

    /// <summary>Property actions receive empty state and output collections for a missing model.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task NullPropertyActionReceivesEmptyCollections()
    {
        using var model = CreateModel(FirstError);
        var view = new TestView(model);
        IList<IValidationState>? lastStates = null;
        IList<string>? lastMessages = null;
        using var binding = ValidationBinding.ForProperty<TestView, TestViewModel, string?, string>(
            view,
            static vm => vm.Name,
            (states, messages) =>
            {
                lastStates = states;
                lastMessages = messages;
            },
            SingleLineFormatter.Default);
        view.ViewModel = null;
        await Assert.That(lastStates).IsNotNull();
        await Assert.That(lastStates!).IsEmpty();
        await Assert.That(lastMessages).IsNotNull();
        await Assert.That(lastMessages!).IsEmpty();
    }

    /// <summary>An action may dispose its own binding while processing a state update.</summary>
    /// <param name="path">The action overload to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.HelperAction)]
    public async Task ActionCanDisposeBindingDuringNotification(BindingPath path)
    {
        using var first = CreateModel(FirstError);
        using var second = CreateModel(SecondError);
        var view = new TestView(first);
        IValidationBinding? binding = null;
        var calls = 0;
        binding = CreateActionBinding(view, path, _ =>
        {
            calls++;
            binding?.Dispose();
        });
        first.Name = ValidName;
        var disposedCalls = calls;
        first.Name = string.Empty;
        view.ViewModel = second;
        second.Name = ValidName;
        await Assert.That(calls).IsEqualTo(disposedCalls);
        await Assert.That(() => binding.Dispose()).ThrowsNothing();
    }

    /// <summary>A cleanup error is reported once and does not leave the binding live for another disposal.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ThrowingCleanupIsNotRetried()
    {
        var cleanupCount = 0;
        var source = new CallbackObservable(() =>
        {
            cleanupCount++;
            throw new InvalidOperationException(FirstError);
        });
        var binding = new ValidationBinding(source);
        await Assert.That(() => binding.Dispose()).Throws<InvalidOperationException>();
        await Assert.That(() => binding.Dispose()).ThrowsNothing();
        await Assert.That(cleanupCount).IsEqualTo(1);
    }

    /// <summary>A detached helper cannot forward late notifications even when its source ignores disposal.</summary>
    /// <param name="path">The helper binding target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.NestedHelper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task DetachedHelperIgnoresLateErrorsCompletionAndStates(BindingPath path)
    {
        using var model = CreateModel(SecondError);
        var replacement = model.NameRule;
        var component = new LateNotificationComponent();
        using var oldHelper = new ValidationHelper(component);
        model.NameRule = oldHelper;
        var view = new TestView(model);
        using var binding = CreateBinding(view, path);
        var detachedObserver = component.LatestObserver!;
        model.NameRule = replacement;
        detachedObserver.OnError(new InvalidOperationException(FirstError));
        detachedObserver.OnCompleted();
        detachedObserver.OnNext(new ValidationState(false, FirstError));
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
        model.Name = ValidName;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
    }

    /// <summary>A binding still follows model changes after its current rule completes.</summary>
    /// <param name="path">The binding overload and target path to exercise.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(BindingPath.Property)]
    [Arguments(BindingPath.Model)]
    [Arguments(BindingPath.PropertyAction)]
    [Arguments(BindingPath.ModelAction)]
    [Arguments(BindingPath.Helper)]
    [Arguments(BindingPath.HelperAction)]
    public async Task CompletedRuleDoesNotCompleteModelObservation(BindingPath path)
    {
        using var first = new TestViewModel();
        using var second = CreateModel(SecondError);
        using var source = new System.Reactive.Subjects.BehaviorSubject<IValidationState>(new ValidationState(false, FirstError));
        using var rule = first.ValidationRule(vm => vm.Name, source);
        first.NameRule = rule;
        var view = new TestView(first);
        using var binding = CreateBinding(view, path);
        await Assert.That(ReadMessage(view, path)).IsEqualTo(FirstError);
        source.OnCompleted();
        view.ViewModel = null;
        await Assert.That(ReadMessage(view, path)).IsEmpty();
        view.ViewModel = second;
        await Assert.That(ReadMessage(view, path)).IsEqualTo(SecondError);
    }

    /// <summary>Creates an action binding whose callback can record or reenter notifications.</summary>
    /// <param name="view">The view whose model is observed.</param>
    /// <param name="path">The action overload to use.</param>
    /// <param name="callback">The callback receiving the formatted state.</param>
    /// <returns>The binding subscription.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The path is not an action overload.</exception>
    private static IValidationBinding CreateActionBinding(TestView view, BindingPath path, Action<string> callback) => path switch
    {
        BindingPath.PropertyAction => ValidationBinding.ForProperty<TestView, TestViewModel, string?, string>(
            view,
            static vm => vm.Name,
            (_, messages) => callback(string.Join(" ", messages)),
            SingleLineFormatter.Default),
        BindingPath.ModelAction => ValidationBinding.ForViewModel<TestView, TestViewModel, string>(view, callback, SingleLineFormatter.Default),
        BindingPath.HelperAction => ValidationBinding.ForValidationHelperProperty<TestView, TestViewModel, string>(
            view,
            static vm => vm!.NameRule,
            (_, message) => callback(message),
            SingleLineFormatter.Default),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    /// <summary>Creates an invalid model with a helper shared by the binding paths.</summary>
    /// <param name="error">The model's error message.</param>
    /// <returns>The model owning its validation context.</returns>
    private static TestViewModel CreateModel(string error)
    {
        var model = new TestViewModel { Name = string.Empty };
        model.NameRule = model.ValidationRuleUnsafe(vm => vm.Name, static name => !string.IsNullOrEmpty(name), error);
        return model;
    }

    /// <summary>Creates one of the public string or action bindings.</summary>
    /// <param name="view">The view to update.</param>
    /// <param name="path">The overload and target path.</param>
    /// <returns>The binding subscription.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The binding path is unknown.</exception>
    private static IValidationBinding CreateBinding(TestView view, BindingPath path) => path switch
    {
        BindingPath.Property => ValidationBinding.ForProperty<TestView, TestViewModel, string?, string>(
            view,
            static vm => vm.Name,
            static v => v.NameErrorLabel),
        BindingPath.NestedProperty => ValidationBinding.ForProperty<TestView, TestViewModel, string?, string>(
            view,
            static vm => vm.Name,
            static v => v.NameErrorContainer.Text,
            SingleLineFormatter.Default,
            false),
        BindingPath.Model => ValidationBinding.ForViewModel<TestView, TestViewModel, string>(
            view,
            static v => v.NameErrorLabel),
        BindingPath.NestedModel => ValidationBinding.ForViewModel<TestView, TestViewModel, string>(
            view,
            static v => v.NameErrorContainer.Text,
            SingleLineFormatter.Default),
        BindingPath.PropertyAction => ValidationBinding.ForProperty<TestView, TestViewModel, string?, string>(
            view,
            static vm => vm.Name,
            (_, messages) => view.NameErrorLabel = string.Join(" ", messages),
            SingleLineFormatter.Default),
        BindingPath.NonStrictPropertyAction => ValidationBinding.ForProperty<TestView, TestViewModel, string?, string>(
            view,
            static vm => vm.Name,
            (_, messages) => view.NameErrorLabel = string.Join(" ", messages),
            SingleLineFormatter.Default,
            false),
        BindingPath.ModelAction => ValidationBinding.ForViewModel<TestView, TestViewModel, string>(
            view,
            message => view.NameErrorLabel = message,
            SingleLineFormatter.Default),
        BindingPath.Helper => ValidationBinding.ForValidationHelperProperty<TestView, TestViewModel, string>(
            view,
            static vm => vm!.NameRule,
            static v => v.NameErrorLabel),
        BindingPath.NestedHelper => ValidationBinding.ForValidationHelperProperty<TestView, TestViewModel, string>(
            view,
            static vm => vm!.NameRule,
            static v => v.NameErrorContainer.Text,
            SingleLineFormatter.Default),
        BindingPath.HelperAction => ValidationBinding.ForValidationHelperProperty<TestView, TestViewModel, string>(
            view,
            static vm => vm!.NameRule,
            (_, message) => view.NameErrorLabel = message,
            SingleLineFormatter.Default),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    /// <summary>Reads the target used by a binding path.</summary>
    /// <param name="view">The updated view.</param>
    /// <param name="path">The overload and target path.</param>
    /// <returns>The current rendered message.</returns>
    private static string ReadMessage(TestView view, BindingPath path) =>
        path is BindingPath.NestedProperty or BindingPath.NestedModel or BindingPath.NestedHelper ? view.NameErrorContainer.Text : view.NameErrorLabel;

    /// <summary>A deliberately reentrant cleanup, without built-in idempotent disposal.</summary>
    /// <param name="callback">The callback to invoke every time disposal is requested.</param>
    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => callback();
    }

    /// <summary>A source returning its raw disposable so built-in disposable wrappers cannot mask reentrancy.</summary>
    /// <param name="cleanup">The cleanup callback.</param>
    private sealed class CallbackObservable(Action cleanup) : IObservable<BindingUnit>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable Subscribe(IObserver<BindingUnit> observer) => new CallbackDisposable(cleanup);
    }

    /// <summary>A component whose observable can deliver notifications to an already disposed observer.</summary>
    private sealed class LateNotificationComponent : IValidationComponent, IObservable<IValidationState>
    {
        /// <summary>Gets the observer most recently attached, which is the binding after helper construction.</summary>
        public IObserver<IValidationState>? LatestObserver { get; private set; }

        /// <inheritdoc/>
        public IValidationText Text { get; } = ValidationText.Create(FirstError);

        /// <inheritdoc/>
        public bool IsValid => false;

        /// <inheritdoc/>
        public IObservable<IValidationState> ValidationStatusChange => this;

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<IValidationState> observer)
        {
            LatestObserver = observer;
            observer.OnNext(new ValidationState(false, FirstError));
            return new CallbackDisposable(static () => { });
        }
    }
}
