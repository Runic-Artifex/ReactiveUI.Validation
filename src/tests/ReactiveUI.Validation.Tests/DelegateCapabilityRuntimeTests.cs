// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

using Disposable = ReactiveUI.Primitives.Disposables.Scope;

#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
namespace ReactiveUI.Validation.Reactive.Tests;
#else
using ReactiveUI.Validation.Capabilities;
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Exercises normal stored callable routes against scoped typed capabilities.</summary>
[ValidationRuntimeDispatch]
public class DelegateCapabilityRuntimeTests
{
    /// <summary>The initial invalid selected value.</summary>
    private const string InvalidValue = "bad";

    /// <summary>The valid selected value.</summary>
    private const string ValidValue = "ok";

    /// <summary>The mutable capture's invalidating suffix.</summary>
    private const string Suffix = "-suffix";

    /// <summary>The static validation message.</summary>
    private const string ErrorMessage = "error";

    /// <summary>The finite callable metadata operation identity.</summary>
    private const string MetadataSchema = "metadata";

    /// <summary>The finite helper operation identity.</summary>
    private const string HelperSchema = "helper";

    /// <summary>The registration capacity used by combined role tests.</summary>
    private const int CatalogCapacity = 4;

    /// <summary>The number of normal callable rule overloads.</summary>
    private const int RuleCount = 4;

    /// <summary>The number of rules in each selected context.</summary>
    private const int ContextRuleCount = 2;

    /// <summary>The second structural property key.</summary>
    private const int SecondKey = 2;

    /// <summary>The caller state revision supplied by a replacement model.</summary>
    private const int ReplacementRevision = 3;

    /// <summary>All four normal rule overloads retain invalid text, captured contexts and live source/capture dependencies.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task FourNormalCallableRulesTrackCurrentOwnersAndCapturedContexts()
    {
        using var original = new ValidationContext(ImmediateSequencer.Instance);
        using var selected = new ValidationContext(ImmediateSequencer.Instance);
        using var replacement = new ValidationContext(ImmediateSequencer.Instance);
        var oldNode = new TrackingNode { Value = InvalidValue };
        var source = new DelegateSource(original) { Node = oldNode };
        var capture = new TrackingNode();
        var currentNode = new TrackingNode { Value = ValidValue };
        Func<DelegateSource, string> callable = capture.Select;
        using var registry = new ValidationPlanRegistry(1);
        using var attachment = registry.Attach(source);
        using var registration = registry.RegisterDelegateSelector(ValidationPlanRole.RuleValue, callable, RuleSelector(capture));
        var first = source.ValidationRule(callable, static value => value == ValidValue, ErrorMessage);
        var second = source.ValidationRule(callable, static value => value == ValidValue, static value => value!);
        var third = source.ValidationRule(selected, callable, static value => value == ValidValue, ErrorMessage);
        var fourth = source.ValidationRule(selected, callable, static value => value == ValidValue, static value => value!);
        try
        {
            await Assert.That(first.IsValid).IsFalse();
            await Assert.That(first.Message[0]).IsEqualTo(ErrorMessage);
            await Assert.That(second.Message[0]).IsEqualTo(InvalidValue);
            await Assert.That(third.IsValid).IsFalse();
            await Assert.That(fourth.Message[0]).IsEqualTo(InvalidValue);
            source.ValidationContext = replacement;
            oldNode.Value = ValidValue;
            await Assert.That(first.IsValid && second.IsValid && third.IsValid && fourth.IsValid).IsTrue();
            capture.Value = Suffix;
            await Assert.That(second.Message[0]).IsEqualTo(ValidValue + Suffix);
            await Assert.That(fourth.Message[0]).IsEqualTo(ValidValue + Suffix);
            source.Node = currentNode;
            capture.Value = string.Empty;
            await Assert.That(first.IsValid && second.IsValid && third.IsValid && fourth.IsValid).IsTrue();
            await Assert.That(oldNode.ListenerCount).IsEqualTo(0);
            await Assert.That(currentNode.ListenerCount).IsEqualTo(RuleCount);
            await Assert.That(capture.ListenerCount).IsEqualTo(RuleCount);
            await Assert.That(original.Validations.Count).IsEqualTo(ContextRuleCount);
            await Assert.That(selected.Validations.Count).IsEqualTo(ContextRuleCount);
            await Assert.That(replacement.Validations.Count).IsEqualTo(0);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
            third.Dispose();
            fourth.Dispose();
        }

        await Assert.That(original.Validations.Count + selected.Validations.Count).IsEqualTo(0);
        await Assert.That(currentNode.ListenerCount + capture.ListenerCount).IsEqualTo(0);
        await Assert.That(capture.SelectionCalls).IsEqualTo(0);
    }

    /// <summary>Normal default and selected-context metadata calls avoid both value reads and value owner acquisition.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CallableMetadataUsesIndependentDependenciesAndPreservesRichStates()
    {
        using var original = new ValidationContext(ImmediateSequencer.Instance);
        using var selected = new ValidationContext(ImmediateSequencer.Instance);
        var oldNode = new TrackingNode();
        var source = new DelegateSource(original) { Node = oldNode, SelectedContext = selected };
        var view = new DelegateView { ViewModel = source };
        var rich = new RichState(1);
        var firstPath = ValidationPath.Structural(nameof(DelegateSource.Node), 1, EqualityComparer<int>.Default);
        var secondPath = ValidationPath.Structural(nameof(DelegateSource.Node), SecondKey, EqualityComparer<int>.Default);
        using var defaultRule = ValidationRuntime.RegisterRule(source, original, ConstantPath(firstPath), _ => rich);
        using var selectedRule = ValidationRuntime.RegisterRule(source, selected, ConstantPath(firstPath), _ => rich);
        Func<DelegateSource, string> exact = static model => model.ExplosiveValue;
        Func<DelegateSource, string> pattern = ReadProperty;
        Func<DelegateSource, IValidationContext?> context = ReadSelectedContext;
        using var registry = new ValidationPlanRegistry(CatalogCapacity);
        using var attachment = registry.Attach(source);
        using var exactLease = registry.RegisterDelegateSelector(ValidationPlanRole.Property, exact, MetadataSelector(firstPath, secondPath));
        using var patternLease = registry.RegisterDelegateSelectorPattern(
            ValidationPlanRole.Property,
            MetadataSchema,
            ValidationDelegatePattern.Create(pattern),
            _ => MetadataSelector(firstPath, secondPath));
        using var contextLease = registry.RegisterDelegateSelector(ValidationPlanRole.Context, context, ContextSelector());
        IList<IValidationState>? defaultStates = null;
        IList<IValidationState>? selectedStates = null;
        var currentNode = new TrackingNode();
        var first = view.BindValidationState(source, exact, static states => states, states => defaultStates = states, true);
        var second = view.BindValidationContext(source, context, pattern, states => selectedStates = states);
        try
        {
            await Assert.That(defaultStates![0]).IsSameReferenceAs(rich);
            await Assert.That(selectedStates![0]).IsSameReferenceAs(rich);
            oldNode.Key = SecondKey;
            await Assert.That(defaultStates!.Count + selectedStates!.Count).IsEqualTo(0);
            source.Node = currentNode;
            await Assert.That(defaultStates![0]).IsSameReferenceAs(rich);
            await Assert.That(selectedStates![0]).IsSameReferenceAs(rich);
            await Assert.That(oldNode.ListenerCount).IsEqualTo(0);
            await Assert.That(currentNode.ListenerCount).IsEqualTo(ContextRuleCount);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }

        await Assert.That(currentNode.ListenerCount).IsEqualTo(0);
        await Assert.That(source.ForbiddenValueReads + source.ForbiddenOwnerReads).IsEqualTo(0);
        await Assert.That(original.Validations.Count + selected.Validations.Count).IsEqualTo(ContextRuleCount);
    }

    /// <summary>Untyped null retains expression overload selection while typed delegate null resolves the separate callable catalog.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task NullSelectionKeepsExpressionAndDelegateCatalogsDistinct()
    {
        using var context = new ValidationContext(ImmediateSequencer.Instance);
        var source = new DelegateSource(context);
        var expressionPlan = ConstantValue(InvalidValue);
        var delegatePlan = ConstantValue(ValidValue);
        using var registry = new ValidationPlanRegistry(ContextRuleCount);
        using var attachment = registry.Attach(source);
        using var oldLease = registry.RegisterSelector(ValidationPlanRole.Model, (Expression<Func<DelegateSource, string>>?)null, expressionPlan);
        using var newLease = registry.RegisterDelegateSelector(ValidationPlanRole.Model, (Func<DelegateSource, string>?)null, delegatePlan);
        var oldSelection = ValidationRuntime.ResolveSelector<DelegateSource, string>(source, null, ValidationPlanRole.Model, string.Empty);
        var newSelection = ValidationRuntime.ResolveSelector(source, (Func<DelegateSource, string>?)null, ValidationPlanRole.Model, string.Empty);
        await Assert.That(oldSelection).IsSameReferenceAs(expressionPlan);
        await Assert.That(newSelection).IsSameReferenceAs(delegatePlan);
        await Assert.That(oldSelection.Bind(source).Read().Value).IsEqualTo(InvalidValue);
        await Assert.That(newSelection.Bind(source).Read().Value).IsEqualTo(ValidValue);
        await Assert.That(source.ForbiddenValueReads).IsEqualTo(0);
    }

    /// <summary>Current model providers precede view providers across replacements while bindings borrow complete helper states.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CallableHelperSwitchesCurrentProvidersWithoutOwningModelsOrRules()
    {
        using var firstContext = new ValidationContext(ImmediateSequencer.Instance);
        using var secondContext = new ValidationContext(ImmediateSequencer.Instance);
        var first = new DelegateSource(firstContext);
        var second = new DelegateSource(secondContext);
        var view = new DelegateView();
        var firstState = new RichState(1);
        var secondState = new RichState(SecondKey);
        var updated = new RichState(ReplacementRevision);
        using var firstStates = new BehaviorSubject<IValidationState>(firstState);
        using var secondStates = new BehaviorSubject<IValidationState>(secondState);
        using var firstRule = first.AddObservableRule(firstStates, []);
        using var secondRule = second.AddObservableRule(secondStates, []);
        first.Helper = firstRule;
        second.Helper = secondRule;
        Func<DelegateSource, ValidationHelper?> callable = ReadHelper;
        using var modelRegistry = new ValidationPlanRegistry(ContextRuleCount);
        using var firstAttachment = modelRegistry.Attach(first);
        using var secondAttachment = modelRegistry.Attach(second);
        var modelFactories = 0;
        using var modelLease = modelRegistry.RegisterDelegateSelectorPattern(ValidationPlanRole.Helper, HelperSchema, ValidationDelegatePattern.Create(callable), _ =>
        {
            modelFactories++;
            return HelperSelector();
        });
        using var viewRegistry = new ValidationPlanRegistry(1);
        using var viewAttachment = viewRegistry.Attach(view);
        var viewFactories = 0;
        using var viewLease = viewRegistry.RegisterDelegateSelectorPattern(ValidationPlanRole.Helper, HelperSchema, ValidationDelegatePattern.Create(callable), _ =>
        {
            viewFactories++;
            return HelperSelector();
        });
        IValidationState? observed = null;
        var binding = view.BindValidationState(first, callable, static state => state, state => observed = state);
        try
        {
            await Assert.That(observed!.IsValid).IsTrue();
            view.ViewModel = first;
            await Assert.That(observed).IsSameReferenceAs(firstState);
            view.ViewModel = second;
            await Assert.That(observed).IsSameReferenceAs(secondState);
            secondStates.OnNext(updated);
            firstStates.OnNext(new RichState(1));
            await Assert.That(observed).IsSameReferenceAs(updated);
            await Assert.That(modelFactories).IsEqualTo(ContextRuleCount);
            await Assert.That(viewFactories).IsEqualTo(0);
        }
        finally
        {
            binding.Dispose();
        }

        secondStates.OnNext(secondState);
        await Assert.That(observed).IsSameReferenceAs(updated);
        await Assert.That(firstContext.Validations.Count + secondContext.Validations.Count).IsEqualTo(ContextRuleCount);
        await Assert.That(first.DisposeCount + second.DisposeCount).IsEqualTo(0);
        await Assert.That(first.ForbiddenValueReads + second.ForbiddenValueReads).IsEqualTo(0);
    }

    /// <summary>Matched incomplete metadata and positive-null delegate providers fail without evaluating opaque callables.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CallableMetadataFailuresNeverInvokeSelectorsOrFallbacks()
    {
        using var context = new ValidationContext(ImmediateSequencer.Instance);
        var source = new DelegateSource(context);
        var view = new DelegateView { ViewModel = source };
        Func<DelegateSource, string> callable = ReadProperty;
        using var registry = new ValidationPlanRegistry(1);
        using var attachment = registry.Attach(view);
        using var lease = registry.RegisterDelegateSelector(ValidationPlanRole.Property, callable, ConstantValue(ValidValue));
        await Assert.That(() => ValidationBinding.ForProperty(view, callable, static (_, _) => { }, new ConstFormatter(ErrorMessage))).Throws<InvalidOperationException>();
        var coldFallback = ConstantValue(InvalidValue);
        await Assert.That(() => ValidationRuntime.ResolveSelector(source, new NullDelegateProvider(), callable, ValidationPlanRole.Property, string.Empty, coldFallback))
            .Throws<InvalidOperationException>();
        await Assert.That(source.ForbiddenValueReads).IsEqualTo(0);
        await Assert.That(context.Validations.Count).IsEqualTo(0);
    }

    /// <summary>Creates direct value access with independently owned current-node and capture notifications.</summary>
    /// <param name="capture">The mutable capture owner.</param>
    /// <returns>The typed value descriptor.</returns>
    private static ValidationSelector<DelegateSource, string> RuleSelector(TrackingNode capture) => new(source => new(
        () => ValidationRead<string>.Present(source.Node.Value + capture.Value, [ValidationPath.Legacy(nameof(DelegateSource.Node))]),
        [
            ValidationDependency.PropertyChanged(() => source, nameof(source.Node)),
            ValidationDependency.PropertyChanged(() => source.Node, nameof(TrackingNode.Value)),
            ValidationDependency.PropertyChanged(() => capture, nameof(capture.Value)),
        ],
        ValidationObservationOptions<string>.Default));

    /// <summary>Creates cached metadata access with separate value-owner dependencies that must stay unused.</summary>
    /// <param name="first">The first structural path.</param>
    /// <param name="second">The second structural path.</param>
    /// <returns>The typed metadata descriptor.</returns>
    private static ValidationSelector<DelegateSource, string> MetadataSelector(ValidationPath first, ValidationPath second) => new(source => ValidationAccessPlan.CreateObservation(() =>
    {
        TrackingNode? cachedOwner = null;
        var forbidden = ValidationDependency.Create<DelegateSource>(
            () =>
            {
                source.ForbiddenOwnerReads++;
                throw new InvalidOperationException("Metadata observed a value dependency owner.");
            },
            static (_, _) => Disposable.Create(static () => { }));
        var afterRead = ValidationDependency.AfterRead(ValidationDependency.PropertyChanged(() => cachedOwner, nameof(TrackingNode.Key)));
        return new ValidationAccessPlan<string>(
            () => ValidationRead<string>.Present(source.ExplosiveValue, []),
            [forbidden],
            ValidationObservationOptions<string>.Default,
            () =>
            {
                cachedOwner = source.Node;
                return [cachedOwner.Key == 1 ? first : second];
            },
            [ValidationDependency.PropertyChanged(() => source, nameof(source.Node)), afterRead]);
    }));

    /// <summary>Creates explicit selected-context access without invoking the opaque callable.</summary>
    /// <returns>The context descriptor.</returns>
    private static ValidationSelector<DelegateSource, IValidationContext?> ContextSelector() => new(source => new(
        () => ValidationRead<IValidationContext?>.Present(source.SelectedContext, []),
        [ValidationDependency.PropertyChanged(() => source, nameof(source.SelectedContext))],
        ValidationObservationOptions<IValidationContext?>.Default));

    /// <summary>Creates typed helper access borrowed from the current model.</summary>
    /// <returns>The helper descriptor.</returns>
    private static ValidationSelector<DelegateSource, ValidationHelper?> HelperSelector() => new(source => new(
        () => ValidationRead<ValidationHelper?>.Present(source.Helper, []),
        [ValidationDependency.PropertyChanged(() => source, nameof(source.Helper))],
        ValidationObservationOptions<ValidationHelper?>.Default));

    /// <summary>Creates a constant complete structural rule snapshot.</summary>
    /// <param name="path">The path matched by metadata bindings.</param>
    /// <returns>The constant value descriptor.</returns>
    private static ValidationSelector<DelegateSource, int> ConstantPath(ValidationPath path) => new(_ => new(
        () => ValidationRead<int>.Present(0, [path]),
        [],
        ValidationObservationOptions<int>.Default));

    /// <summary>Creates distinct implicit selector descriptors without accessing a source.</summary>
    /// <param name="value">The selected value.</param>
    /// <returns>The constant value descriptor.</returns>
    private static ValidationSelector<DelegateSource, string> ConstantValue(string value) => new(_ => new(
        () => ValidationRead<string>.Present(value, []),
        [],
        ValidationObservationOptions<string>.Default));

    /// <summary>An opaque property selection that must never execute during metadata dispatch.</summary>
    /// <param name="source">The current model.</param>
    /// <returns>The selected value if deliberately invoked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadProperty(DelegateSource source) => source.ExplosiveValue;

    /// <summary>An opaque selected-context method group resolved through its registration.</summary>
    /// <param name="source">The current model.</param>
    /// <returns>The selected context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IValidationContext? ReadSelectedContext(DelegateSource source) => source.SelectedContext;

    /// <summary>An opaque helper method group that must not execute during typed dispatch.</summary>
    /// <param name="source">The current model.</param>
    /// <returns>The selected helper if deliberately invoked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValidationHelper? ReadHelper(DelegateSource source) => source.ExplosiveHelper;

    /// <summary>A complete invalid state carrying caller-owned metadata.</summary>
    /// <param name="revision">The caller's revision.</param>
    private sealed class RichState(int revision) : IValidationState
    {
        /// <inheritdoc />
        public bool IsValid => false;

        /// <inheritdoc />
        public IValidationText Text { get; } = ValidationText.Create(ErrorMessage);

        /// <summary>Gets the caller's metadata revision.</summary>
        public int Revision { get; } = revision;
    }

    /// <summary>A model whose contexts, helper and selected node can be replaced independently.</summary>
    /// <param name="context">The initial default context.</param>
    private sealed class DelegateSource(IValidationContext context) : ReactiveObject, IValidatableViewModel, IDisposable
    {
        /// <inheritdoc />
        public IValidationContext ValidationContext
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = context;

        /// <summary>Gets or sets the explicitly selected advisory context.</summary>
        public IValidationContext? SelectedContext
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        /// <summary>Gets or sets the selected nested owner.</summary>
        public TrackingNode Node
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = new();

        /// <summary>Gets or sets the borrowed helper.</summary>
        public ValidationHelper? Helper
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        /// <summary>Gets or sets the number of forbidden value-owner reads.</summary>
        public int ForbiddenOwnerReads { get; set; }

        /// <summary>Gets the number of forbidden selected value reads.</summary>
        public int ForbiddenValueReads { get; private set; }

        /// <summary>Gets the number of disposal attempts made on this borrowed model.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>Gets a leaf that metadata must never evaluate.</summary>
        /// <exception cref="InvalidOperationException">The forbidden getter was invoked.</exception>
        public string ExplosiveValue
        {
            get
            {
                ForbiddenValueReads++;
                throw new InvalidOperationException("Opaque metadata callable was invoked.");
            }
        }

        /// <summary>Gets a helper through an opaque callable that dispatch must never invoke.</summary>
        /// <exception cref="InvalidOperationException">The opaque helper getter was invoked.</exception>
        public ValidationHelper? ExplosiveHelper
        {
            get
            {
                ForbiddenValueReads++;
                throw new InvalidOperationException("Opaque helper callable was invoked.");
            }
        }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>A notifying owner usable as both a nested value source and mutable method-group capture.</summary>
    private sealed class TrackingNode : INotifyPropertyChanged
    {
        /// <summary>The current notification handlers.</summary>
        private PropertyChangedEventHandler? _changed;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        /// <summary>Gets the current listener count.</summary>
        public int ListenerCount => _changed?.GetInvocationList().Length ?? 0;

        /// <summary>Gets the number of opaque method-group invocations.</summary>
        public int SelectionCalls { get; private set; }

        /// <summary>Gets or sets the selected or captured value.</summary>
        public string Value
        {
            get;
            set
            {
                field = value;
                _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        } = string.Empty;

        /// <summary>Gets or sets the structural property identity.</summary>
        public int Key
        {
            get;
            set
            {
                field = value;
                _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Key)));
            }
        } = 1;

        /// <summary>Defines the opaque callable without serving as the typed descriptor's getter.</summary>
        /// <param name="source">The current source.</param>
        /// <returns>The selected source and capture value.</returns>
        public string Select(DelegateSource source)
        {
            SelectionCalls++;
            return source.Node.Value + Value;
        }
    }

    /// <summary>A notifying view whose current model is supplied by the view contract.</summary>
    private sealed class DelegateView : ReactiveObject, IViewFor<DelegateSource>
    {
        /// <inheritdoc />
        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DelegateSource?)value;
        }

        /// <inheritdoc />
        public DelegateSource? ViewModel
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    /// <summary>A deliberately invalid provider whose positive result lacks a descriptor.</summary>
    private sealed class NullDelegateProvider : IValidationDelegatePlanProvider
    {
        /// <inheritdoc />
        public bool TryGetDelegateSelector<TSource, TValue>(ValidationPlanRequest request, Func<TSource, TValue>? selection, [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector)
        {
            // Foreign providers can violate annotations; resolution must reject the positive-null result.
            selector = null!;
            return true;
        }

        /// <inheritdoc />
        public bool TryGetDelegateTarget<TSource, TValue, TOut>(ValidationPlanRequest request, Func<TSource, TValue>? selection, [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target)
        {
            target = null;
            return false;
        }
    }
}
