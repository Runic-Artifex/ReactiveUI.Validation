// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Executes typed validation capabilities without expression compilation or member discovery.</summary>
public static partial class ValidationRuntime
{
    /// <summary>Resolves an explicitly registered selector for a normal expression API.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The source that supplies or owns the provider.</param>
    /// <param name="expression">The current expression, including current captured arguments.</param>
    /// <param name="role">The selector's purpose.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>The typed descriptor.</returns>
    /// <exception cref="InvalidOperationException">No matching typed capability is registered.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationSelector<TSource, TValue> ResolveSelector<TSource, TValue>(
        TSource source,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity) => ResolveSelectorCore(source, source, expression, role, operationIdentity);

    /// <summary>Resolves a current source capability with an explicitly selected fallback provider owner.</summary>
    /// <typeparam name="TSource">The original selected source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current selected source.</param>
    /// <param name="providerOwner">The fallback provider or registry owner, such as the view.</param>
    /// <param name="expression">The current invocation expression.</param>
    /// <param name="role">The expression's purpose.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>The exact typed descriptor, preferring the source's current provider.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationSelector<TSource, TValue> ResolveSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity) => ResolveSelectorCore(source, providerOwner, expression, role, operationIdentity);

    /// <summary>Prefers an explicitly registered selector and otherwise uses a compiled typed descriptor.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current receiver supplied to the selected descriptor.</param>
    /// <param name="providerOwner">The fallback provider owner, such as the view.</param>
    /// <param name="expression">The current invocation expression.</param>
    /// <param name="role">The exact selector role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="generatedFallback">The cold compiled descriptor used only when lookup finds no match.</param>
    /// <returns>The registered descriptor or the supplied compiled descriptor.</returns>
    /// <remarks>Provider and catalog failures, ambiguity and successful null results propagate. They never select the fallback.</remarks>
    public static ValidationSelector<TSource, TValue> ResolveSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity,
        ValidationSelector<TSource, TValue> generatedFallback)
    {
        ArgumentExceptionHelper.ThrowIfNull(generatedFallback);
        return TryResolveSelectorCore(source, providerOwner, expression, role, operationIdentity, out var selected)
            ? selected ?? throw MissingPlan(role)
            : generatedFallback;
    }

    /// <summary>Resolves a structural property identity without evaluating the expression's selected value.</summary>
    /// <typeparam name="TSource">The original selected source API type.</typeparam>
    /// <typeparam name="TValue">The expression's original value type.</typeparam>
    /// <param name="source">The current selected source.</param>
    /// <param name="providerOwner">The fallback provider or registry owner.</param>
    /// <param name="expression">The current original property expression.</param>
    /// <param name="role">The expression's registered metadata role.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>A descriptor observing exactly one explicit property identity with the same typed dependencies.</returns>
    /// <exception cref="InvalidOperationException">The bound plan has no metadata reader or does not select exactly one path.</exception>
    public static ValidationSelector<TSource, ValidationPath> ResolvePathSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity)
    {
        var selector = ResolveSelectorCore(source, providerOwner, expression, role, operationIdentity);
        return new(current => selector.Bind(current).CreatePathPlan());
    }

    /// <summary>Prefers registered metadata and otherwise uses a compiled metadata-only descriptor.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The original expression's selected member type.</typeparam>
    /// <param name="source">The current receiver.</param>
    /// <param name="providerOwner">The fallback provider owner.</param>
    /// <param name="expression">The current property expression.</param>
    /// <param name="role">The exact metadata role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="generatedPathFallback">The cold compiled path descriptor, which never reads the selected leaf.</param>
    /// <returns>The registered metadata descriptor or the supplied compiled path descriptor.</returns>
    /// <remarks>Matched descriptor failures, including missing metadata readers, propagate rather than selecting fallback metadata.</remarks>
    public static ValidationSelector<TSource, ValidationPath> ResolvePathSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity,
        ValidationSelector<TSource, ValidationPath> generatedPathFallback)
    {
        ArgumentExceptionHelper.ThrowIfNull(generatedPathFallback);
        if (!TryResolveSelectorCore(source, providerOwner, expression, role, operationIdentity, out var selected))
        {
            return generatedPathFallback;
        }

        var selector = selected ?? throw MissingPlan(role);
        return new(current => selector.Bind(current).CreatePathPlan());
    }

    /// <summary>Resolves an explicitly registered writable target, including a caller-supplied conversion.</summary>
    /// <typeparam name="TSource">The original target source API type.</typeparam>
    /// <typeparam name="TValue">The target expression's value type.</typeparam>
    /// <typeparam name="TOut">The actual presentation output type.</typeparam>
    /// <param name="source">The source that supplies or owns the provider.</param>
    /// <param name="expression">The current target expression.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>The typed writable descriptor.</returns>
    /// <exception cref="InvalidOperationException">No matching typed capability is registered.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage(
        "Design",
        "SST2307:Type parameter is not inferable",
        Justification = "Output may differ from the expression value type; explicit closed output types select the registered conversion contract.")]
    public static ValidationTarget<TSource, TOut> ResolveTarget<TSource, TValue, TOut>(
        TSource source,
        Expression<Func<TSource, TValue>>? expression,
        string operationIdentity) =>
        TryResolveTargetCore(source, expression, operationIdentity, out ValidationTarget<TSource, TOut>? target)
            ? target ?? throw MissingPlan(ValidationPlanRole.Target)
            : throw MissingPlan(ValidationPlanRole.Target);

    /// <summary>Prefers an explicitly registered target and otherwise uses a compiled typed write descriptor.</summary>
    /// <typeparam name="TSource">The original target source API type.</typeparam>
    /// <typeparam name="TValue">The target expression's original member type.</typeparam>
    /// <typeparam name="TOut">The presentation output type.</typeparam>
    /// <param name="source">The current target receiver.</param>
    /// <param name="expression">The current target expression.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="generatedFallback">The cold compiled target used only when lookup finds no match.</param>
    /// <returns>The registered write descriptor or the supplied compiled descriptor.</returns>
    /// <remarks>Provider and catalog failures, ambiguity and successful null results propagate. They never select the fallback.</remarks>
    public static ValidationTarget<TSource, TOut> ResolveTarget<TSource, TValue, TOut>(
        TSource source,
        Expression<Func<TSource, TValue>>? expression,
        string operationIdentity,
        ValidationTarget<TSource, TOut> generatedFallback)
    {
        ArgumentExceptionHelper.ThrowIfNull(generatedFallback);
        return TryResolveTargetCore(source, expression, operationIdentity, out ValidationTarget<TSource, TOut>? target)
            ? target ?? throw MissingPlan(ValidationPlanRole.Target)
            : generatedFallback;
    }

    /// <summary>Registers a typed rule and captures its destination for cleanup.</summary>
    /// <typeparam name="TSource">The borrowed source type.</typeparam>
    /// <typeparam name="TValue">The selected rule value type.</typeparam>
    /// <param name="source">The borrowed validation source.</param>
    /// <param name="context">The destination context.</param>
    /// <param name="selector">The typed value and metadata descriptor.</param>
    /// <param name="validate">Produces the complete rule state.</param>
    /// <returns>A helper owning this rule's registration and subscriptions.</returns>
    public static ValidationHelper RegisterRule<TSource, TValue>(
        TSource source,
        IValidationContext context,
        ValidationSelector<TSource, TValue> selector,
        Func<TValue, IValidationState> validate)
    {
        ArgumentExceptionHelper.ThrowIfNull(context);
        ArgumentExceptionHelper.ThrowIfNull(selector);
        ArgumentExceptionHelper.ThrowIfNull(validate);
        return ValidationRuleContextExtensions.RegisterValidation(context, new SelectorValidation<TSource, TValue>(source, selector, validate));
    }

    /// <summary>Projects values synchronously through a typed callback.</summary>
    /// <typeparam name="TIn">The source value type.</typeparam>
    /// <typeparam name="TOut">The presentation output type.</typeparam>
    /// <param name="source">The borrowed observable.</param>
    /// <param name="project">The typed projection.</param>
    /// <returns>The cold projected observable.</returns>
    public static IObservable<TOut> Project<TIn, TOut>(IObservable<TIn> source, Func<TIn, TOut> project)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(project);
        return source.Select(project);
    }

    /// <summary>Formats every complete rule state's text in its original order.</summary>
    /// <typeparam name="TOut">The inferred presentation output type.</typeparam>
    /// <param name="states">The complete matching states.</param>
    /// <param name="formatter">The explicitly selected typed formatter.</param>
    /// <returns>The ordered formatted outputs, without reconstructing the original states.</returns>
    public static IList<TOut> FormatAll<TOut>(IList<IValidationState> states, IValidationTextFormatter<TOut> formatter)
    {
        ArgumentExceptionHelper.ThrowIfNull(states);
        ArgumentExceptionHelper.ThrowIfNull(formatter);
        var formatted = new List<TOut>(states.Count);
        foreach (var state in states)
        {
            formatted.Add(formatter.Format(state.Text));
        }

        return formatted;
    }

    /// <summary>Subscribes a typed presentation callback, owning only the resulting subscription.</summary>
    /// <typeparam name="TOut">The presentation output type.</typeparam>
    /// <param name="source">The borrowed presentation observable.</param>
    /// <param name="onNext">The synchronous callback.</param>
    /// <returns>The binding's owned subscription.</returns>
    public static IValidationBinding Bind<TOut>(IObservable<TOut> source, Action<TOut> onNext)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(onNext);
        return new ValidationBinding(source.Do(onNext).Select(static _ => Unit.Default));
    }

    /// <summary>Observes the currently selected complete state stream and detaches replaced streams.</summary>
    /// <typeparam name="TSource">The source's original API type.</typeparam>
    /// <param name="source">The borrowed source.</param>
    /// <param name="states">The descriptor selecting the current state observable.</param>
    /// <param name="initial">Explicit presentation prelude policy.</param>
    /// <returns>Actual complete states, or a valid state for ordinary absence. Suppressed absence detaches the previous stream and emits no presentation value.</returns>
    public static IObservable<IValidationState> ObserveState<TSource>(
        TSource source,
        ValidationSelector<TSource, IObservable<IValidationState>?> states,
        ValidationInitialSequence initial)
    {
        ArgumentExceptionHelper.ThrowIfNull(states);
        ValidateInitial(initial);
        return ObserveOwnership(states.Bind(source), ReferenceModelComparer<IObservable<IValidationState>>.Instance)
            .Select(read => read.SuppressMissing
                ? Observable.Empty<IValidationState>()
                : PreludeState(read.Value ?? Observable.Return(ValidationState.Valid), initial))
            .SwitchTo();
    }

    /// <summary>Observes rules matching the currently selected context and structural property path.</summary>
    /// <typeparam name="TSource">The source's original API type.</typeparam>
    /// <param name="source">The borrowed source.</param>
    /// <param name="contexts">The descriptor selecting the context.</param>
    /// <param name="path">The descriptor selecting the current exact property identity.</param>
    /// <param name="strict">Whether matching rules must validate this property exclusively.</param>
    /// <param name="initial">Explicit presentation prelude policy.</param>
    /// <returns>Complete matching states, or an empty collection for ordinary absence. Suppressed absence detaches previous rules and emits no presentation value.</returns>
    public static IObservable<IList<IValidationState>> ObserveProperty<TSource>(
        TSource source,
        ValidationSelector<TSource, IValidationContext?> contexts,
        ValidationSelector<TSource, ValidationPath> path,
        bool strict,
        ValidationInitialSequence initial)
    {
        ArgumentExceptionHelper.ThrowIfNull(contexts);
        ArgumentExceptionHelper.ThrowIfNull(path);
        ValidateInitial(initial);
        return ObserveOwnership(contexts.Bind(source), ReferenceModelComparer<IValidationContext>.Instance)
            .CombineLatest(
                ObserveOwnership(path.Bind(source), null),
                static (context, selectedPath) => (Context: context.Value, Path: selectedPath.Value, SuppressMissing: context.SuppressMissing || selectedPath.SuppressMissing))
            .DistinctUntilChanged(ContextPathComparer.Instance)
            .Select(pair => pair.Context is null || pair.Path is null
                ? MissingProperties(pair.SuppressMissing)
                : ObserveMatchingRules(pair.Context, pair.Path, strict, initial))
            .SwitchTo();
    }

    /// <summary>Observes a model capability through the view's current registered model selection.</summary>
    /// <typeparam name="TView">The original view API type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="TSelected">The selected helper, context or state source type.</typeparam>
    /// <param name="view">The borrowed view or stable storage adapter.</param>
    /// <param name="selection">The typed model selection descriptor.</param>
    /// <param name="stateStream">Selects complete states from each current value.</param>
    /// <param name="initial">The explicit presentation prelude policy.</param>
    /// <returns>Complete current states, or a valid state for ordinary owner absence. Suppressed absence detaches the previous stream and emits no presentation value.</returns>
    public static IObservable<IValidationState> ObserveViewState<TView, TModel, TSelected>(
        TView view,
        ValidationSelector<TModel, TSelected> selection,
        Func<TSelected, IObservable<IValidationState>?> stateStream,
        ValidationInitialSequence initial)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(selection);
        ArgumentExceptionHelper.ThrowIfNull(stateStream);
        ValidateInitial(initial);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingState(model.SuppressMissing)
                : ObserveSelectedOwnership(selection.Bind(model.Value))
                    .Select(read => read.SuppressMissing
                        ? Observable.Empty<IValidationState>()
                        : PreludeState(ReadSelectedState(read, stateStream), initial))
                    .SwitchTo())
            .SwitchTo();
    }

    /// <summary>Observes current model rule membership through a view's registered model selection.</summary>
    /// <typeparam name="TView">The original view API type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="view">The borrowed view or stable storage adapter.</param>
    /// <param name="contexts">The typed model context descriptor.</param>
    /// <param name="path">The typed model property-identity descriptor.</param>
    /// <param name="strict">Whether matching rules must validate only this property.</param>
    /// <param name="initial">The explicit presentation prelude policy.</param>
    /// <returns>Complete matching states, or an empty collection for ordinary model absence. Suppressed absence detaches previous rules and emits no presentation value.</returns>
    public static IObservable<IList<IValidationState>> ObserveViewProperty<TView, TModel>(
        TView view,
        ValidationSelector<TModel, IValidationContext?> contexts,
        ValidationSelector<TModel, ValidationPath> path,
        bool strict,
        ValidationInitialSequence initial)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(contexts);
        ArgumentExceptionHelper.ThrowIfNull(path);
        ValidateInitial(initial);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingProperties(model.SuppressMissing)
                : ObserveProperty(model.Value, contexts, path, strict, initial))
            .SwitchTo();
    }

    /// <summary>Creates the typed default-context selection without an additional owner provider.</summary>
    /// <typeparam name="TModel">The original validatable model API type.</typeparam>
    /// <returns>The typed model context selector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage(
        "Design",
        "SST2307:Type parameter is not inferable",
        Justification = "The descriptor factory preserves the original model API slot before a current receiver is available.")]
    public static ValidationSelector<TModel, IValidationContext?> DefaultContext<TModel>()
        where TModel : class, IValidatableViewModel => DefaultContext<TModel>(null);

    /// <summary>Creates the typed implicit default-context selection with optional replacement notifications.</summary>
    /// <typeparam name="TModel">The original validatable model API type.</typeparam>
    /// <param name="owner">The optional view or host providing an attached typed capability.</param>
    /// <returns>A typed selector using model provider, model attachment, owner provider, owner attachment, then the known model contract.</returns>
    /// <exception cref="InvalidOperationException">A provider reports success with a null default-context descriptor.</exception>
    /// <remarks>
    /// Registered DefaultContext descriptors use a null selector in either provider domain and reference context identity.
    /// The fallback observes INPC when available and otherwise reads a snapshot.
    /// </remarks>
    [SuppressMessage(
        "Design",
        "SST2307:Type parameter is not inferable",
        Justification = "The descriptor factory preserves the original model API slot before a current receiver is available.")]
    public static ValidationSelector<TModel, IValidationContext?> DefaultContext<TModel>(object? owner)
        where TModel : class, IValidatableViewModel => new(model =>
        {
            var request = new ValidationPlanRequest(ValidationPlanRole.DefaultContext, string.Empty);
            if (TryResolveSelectorOn<TModel, IValidationContext?>(model, request, null, out var selected)
                || TryResolveSelectorOn(owner, request, null, out selected))
            {
                return (selected ?? throw MissingPlan(ValidationPlanRole.DefaultContext)).Bind(model)
                    .WithComparer(ReferenceModelComparer<IValidationContext>.Instance);
            }

            var dependencies = model is INotifyPropertyChanged notifyingModel
                ? new[] { ValidationDependency.PropertyChanged(() => notifyingModel, nameof(IValidatableViewModel.ValidationContext)) }
                : Array.Empty<ValidationDependency>();
            return new(
                () => ValidationRead<IValidationContext?>.Present(model.ValidationContext, []),
                dependencies,
                new(ValidationMissingOwnerPolicy.DefaultValue, null, ReferenceModelComparer<IValidationContext>.Instance, false));
        });

    /// <summary>Observes the registered current model selection, or a notifying view's known typed model property.</summary>
    /// <typeparam name="TView">The original view API type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="view">The borrowed view or explicitly owned storage adapter.</param>
    /// <returns>Current real models with reference-identity replacement semantics; an absent owner emits null even when presentation suppression is selected.</returns>
    /// <exception cref="InvalidOperationException">The view requires an explicit observation or stable-storage descriptor.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage(
        "Design",
        "SST2307:Type parameter is not inferable",
        Justification = "TModel is reachable only through the view constraint; preserving the exact registered TView source type requires explicit model arguments.")]
    public static IObservable<TModel?> ObserveModels<TView, TModel>(TView view)
        where TView : IViewFor<TModel>
        where TModel : class =>
        ObserveModelReads<TView, TModel>(view).Select(static read => read.Value);

    /// <summary>Resolves against the current selected source, then its owning view.</summary>
    /// <typeparam name="TSource">The original source type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current selected source.</param>
    /// <param name="owner">The fallback registry owner.</param>
    /// <param name="expression">The current expression.</param>
    /// <param name="role">The expression role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <returns>The exact typed descriptor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValidationSelector<TSource, TValue> ResolveSelectorCore<TSource, TValue>(
        TSource source,
        object? owner,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity) =>
        TryResolveSelectorCore(source, owner, expression, role, operationIdentity, out var selector)
            ? selector ?? throw MissingPlan(role)
            : throw MissingPlan(role);

    /// <summary>Observes the current default context and its complete state.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="view">The borrowed view.</param>
    /// <returns>Current complete aggregate states.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<IValidationState> ObserveModelState<TView, TModel>(TView view)
        where TView : IViewFor<TModel>
        where TModel : class, IValidatableViewModel =>
        ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingState(model.SuppressMissing)
                : ObserveDefaultContext(model.Value, view)
                    .Select(static context => context.Value is null ? MissingState(context.SuppressMissing) : context.Value.ValidationStatusChange)
                    .SwitchTo())
            .SwitchTo();

    /// <summary>Observes the current registered helper and its complete states.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="expression">The expression contract.</param>
    /// <returns>The current helper states.</returns>
    internal static IObservable<IValidationState> ObserveModelHelper<TView, TModel>(
        TView view,
        Expression<Func<TModel, ValidationHelper?>> expression)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(expression);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingState(model.SuppressMissing)
                : ObserveOwnership(
                        ResolveSelectorCore(model.Value, view, expression, ValidationPlanRole.Helper, string.Empty).Bind(model.Value),
                        ReferenceModelComparer<ValidationHelper>.Instance)
                    .Select(static read => read.Value is null ? MissingState(read.SuppressMissing) : read.Value.ValidationChanged)
                    .SwitchTo())
            .SwitchTo();
    }

    /// <summary>Observes the current registered context selection.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="expression">The expression contract.</param>
    /// <returns>Current contexts, including null.</returns>
    internal static IObservable<IValidationContext?> ObserveModelContext<TView, TModel>(
        TView view,
        Expression<Func<TModel, IValidationContext?>> expression)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(expression);
        return ObserveModelContextReads(view, expression).Select(static read => read.Value);
    }

    /// <summary>Observes selected context states while retaining missing-owner presentation policy.</summary>
    /// <typeparam name="TView">The original view type.</typeparam>
    /// <typeparam name="TModel">The current model type.</typeparam>
    /// <param name="view">The borrowed view.</param>
    /// <param name="expression">The registered context selection.</param>
    /// <returns>Complete states, with old subscriptions detached during suppressed absence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<IValidationState> ObserveModelContextState<TView, TModel>(
        TView view,
        Expression<Func<TModel, IValidationContext?>> expression)
        where TView : IViewFor<TModel>
        where TModel : class =>
        ObserveModelContextReads(view, expression)
            .Select(static read => read.Value is null ? MissingState(read.SuppressMissing) : read.Value.ValidationStatusChange)
            .SwitchTo();

    /// <summary>Observes the current registered metadata in the default context.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <typeparam name="TValue">The typed value contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="expression">The expression contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>Matching complete states.</returns>
    internal static IObservable<IList<IValidationState>> ObserveModelProperty<TView, TModel, TValue>(
        TView view,
        Expression<Func<TModel, TValue>> expression,
        bool strict,
        ValidationInitialSequence initial)
        where TView : IViewFor<TModel>
        where TModel : class, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(expression);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingProperties(model.SuppressMissing)
                : ObserveDefaultContext(model.Value, view)
                    .CombineLatest(
                        ResolveSelectorCore(model.Value, view, expression, ValidationPlanRole.Property, string.Empty).Bind(model.Value).ObservePaths(),
                        static (context, paths) => (Context: context.Value, Path: SinglePath(paths), context.SuppressMissing))
                    .DistinctUntilChanged(ContextPathComparer.Instance)
                    .Select(pair => pair.Context is null
                        ? MissingProperties(pair.SuppressMissing)
                        : ObserveMatchingRules(pair.Context, pair.Path, strict, initial))
                    .SwitchTo())
            .SwitchTo();
    }

    /// <summary>Observes the current registered metadata in an explicitly selected context.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <typeparam name="TValue">The typed value contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="contextExpression">The contextExpression contract.</param>
    /// <param name="expression">The expression contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>Matching complete states.</returns>
    internal static IObservable<IList<IValidationState>> ObserveContextProperty<TView, TModel, TValue>(
        TView view,
        Expression<Func<TModel, IValidationContext?>> contextExpression,
        Expression<Func<TModel, TValue>> expression,
        bool strict,
        ValidationInitialSequence initial)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(contextExpression);
        ArgumentExceptionHelper.ThrowIfNull(expression);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingProperties(model.SuppressMissing)
                : ObserveOwnership(
                        ResolveSelectorCore(model.Value, view, contextExpression, ValidationPlanRole.Context, string.Empty).Bind(model.Value),
                        ReferenceModelComparer<IValidationContext>.Instance)
                    .CombineLatest(
                        ResolveSelectorCore(model.Value, view, expression, ValidationPlanRole.Property, string.Empty).Bind(model.Value).ObservePaths(),
                        static (context, paths) => (Context: context.Value, Path: SinglePath(paths), context.SuppressMissing))
                    .DistinctUntilChanged(ContextPathComparer.Instance)
                    .Select(pair => pair.Context is null
                        ? MissingProperties(pair.SuppressMissing)
                        : ObserveMatchingRules(pair.Context, pair.Path, strict, initial))
                    .SwitchTo())
            .SwitchTo();
    }

    /// <summary>Resolves only actual registered selector matches without disguising provider or factory failures as absence.</summary>
    /// <typeparam name="TSource">The original source type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current receiver.</param>
    /// <param name="owner">The fallback provider owner.</param>
    /// <param name="expression">The current invocation expression.</param>
    /// <param name="role">The exact selector role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="selector">The descriptor supplied by a successful match.</param>
    /// <returns>Whether lookup reported an actual match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryResolveSelectorCore<TSource, TValue>(
        TSource source,
        object? owner,
        Expression<Func<TSource, TValue>>? expression,
        ValidationPlanRole role,
        string operationIdentity,
        out ValidationSelector<TSource, TValue>? selector)
    {
        var request = new ValidationPlanRequest(role, operationIdentity);
        return TryResolveSelectorOn(source, request, expression, out selector)
            || (!ReferenceEquals(source, owner) && TryResolveSelectorOn(owner, request, expression, out selector));
    }

    /// <summary>Resolves only actual registered target matches, retaining all provider and catalog failure semantics.</summary>
    /// <typeparam name="TSource">The original target source type.</typeparam>
    /// <typeparam name="TValue">The original expression's member type.</typeparam>
    /// <typeparam name="TOut">The selected output type.</typeparam>
    /// <param name="source">The current receiver and provider owner.</param>
    /// <param name="expression">The current target expression.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="target">The exact typed descriptor supplied by a successful match.</param>
    /// <returns>Whether lookup reported an actual match.</returns>
    private static bool TryResolveTargetCore<TSource, TValue, TOut>(
        TSource source,
        Expression<Func<TSource, TValue>>? expression,
        string operationIdentity,
        out ValidationTarget<TSource, TOut>? target)
    {
        var request = new ValidationPlanRequest(ValidationPlanRole.Target, operationIdentity);
        if (source is IValidationPlanProvider provider && provider.TryGetTarget(request, expression, out target))
        {
            return true;
        }

        var attached = source is null ? null : ValidationPlanRegistry.TryGetAttached(source);
        if (attached is not null && attached.TryGetTarget(request, expression, out target))
        {
            return true;
        }

        target = null;
        return false;
    }

    /// <summary>Observes real model ownership, retaining the presentation policy of missing parents.</summary>
    /// <typeparam name="TView">The original view type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="view">The borrowed view.</param>
    /// <returns>Current model ownership frames.</returns>
    /// <exception cref="InvalidOperationException">The view lacks a legal observation descriptor.</exception>
    private static IObservable<ValidationOwnerRead<TModel?>> ObserveModelReads<TView, TModel>(TView view)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        var request = new ValidationPlanRequest(ValidationPlanRole.Model, string.Empty);
        if (TryResolveSelectorOn<TView, TModel?>(view, request, null, out var selector))
        {
            return ObserveOwnership((selector ?? throw MissingPlan(ValidationPlanRole.Model)).Bind(view), ReferenceModelComparer<TModel>.Instance);
        }

        if (typeof(TView).IsValueType || view is not INotifyPropertyChanged notifyingView)
        {
            throw MissingPlan(ValidationPlanRole.Model);
        }

        var plan = new ValidationAccessPlan<TModel?>(
            () => ValidationRead<TModel?>.Present(view.ViewModel, []),
            [ValidationDependency.PropertyChanged(() => notifyingView, nameof(IViewFor<>.ViewModel))],
            new(ValidationMissingOwnerPolicy.DefaultValue, null, ReferenceModelComparer<TModel>.Instance, false));
        return ObserveOwnership(plan, ReferenceModelComparer<TModel>.Instance);
    }

    /// <summary>Observes selected contexts through each real current model.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="view">The borrowed view.</param>
    /// <param name="expression">The current context expression.</param>
    /// <returns>Context ownership frames that detach old contexts on every missing selection.</returns>
    private static IObservable<ValidationOwnerRead<IValidationContext?>> ObserveModelContextReads<TView, TModel>(
        TView view,
        Expression<Func<TModel, IValidationContext?>> expression)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(expression);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? Observable.Return(new ValidationOwnerRead<IValidationContext?>(model.HasOwner, null, model.SuppressMissing))
                : ObserveOwnership(
                    ResolveSelectorCore(model.Value, view, expression, ValidationPlanRole.Context, string.Empty).Bind(model.Value),
                    ReferenceModelComparer<IValidationContext>.Instance))
            .SwitchTo();
    }

    /// <summary>Retains ownership invalidations while preserving the original missing-owner presentation policy.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="plan">The bound selection plan.</param>
    /// <param name="comparer">Role-specific identity, or null to preserve the declared value comparer.</param>
    /// <returns>A cold stream of real ownership frames, never selecting a synthetic fallback owner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<ValidationOwnerRead<TValue>> ObserveOwnership<TValue>(
        ValidationAccessPlan<TValue> plan,
        IEqualityComparer<TValue>? comparer) => ObserveOwnership(plan, comparer, true);

    /// <summary>Retains missing ownership frames while choosing ownership or ordinary-value fallback semantics.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="plan">The bound selection plan.</param>
    /// <param name="comparer">Role identity, or null to retain declared equality.</param>
    /// <param name="requireOwner">Whether missing selections must discard fallback owner values.</param>
    /// <returns>Current ownership and presentation policy frames.</returns>
    private static IObservable<ValidationOwnerRead<TValue>> ObserveOwnership<TValue>(
        ValidationAccessPlan<TValue> plan,
        IEqualityComparer<TValue>? comparer,
        bool requireOwner)
    {
        var owned = comparer is null ? plan : plan.WithComparer(comparer);
        var suppress = plan.Options.MissingOwner == ValidationMissingOwnerPolicy.Suppress;
        return owned.ObserveIncludingMissing()
            .Select(read => new ValidationOwnerRead<TValue>(
                read.HasOwner,
                read.HasOwner || !requireOwner ? read.Value : default!,
                !read.HasOwner && suppress));
    }

    /// <summary>Preserves arbitrary value equality while selecting known owner values by reference identity.</summary>
    /// <typeparam name="TValue">The inferred selected value type.</typeparam>
    /// <param name="plan">The bound model selection plan.</param>
    /// <returns>A cold selected ownership stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<ValidationOwnerRead<TValue>> ObserveSelectedOwnership<TValue>(ValidationAccessPlan<TValue> plan) =>
        ObserveOwnership(plan, new SelectedOwnerComparer<TValue>(plan.Options.Comparer), SelectedOwnerComparer<TValue>.IsOwnerSelection);

    /// <summary>Projects a selected real owner or a policy-adjusted ordinary value to its state stream.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="read">The current ownership frame.</param>
    /// <param name="stateStream">The caller's typed state selection.</param>
    /// <returns>The actual selected stream, or normal valid state when owner selection is absent.</returns>
    private static IObservable<IValidationState> ReadSelectedState<TValue>(
        ValidationOwnerRead<TValue> read,
        Func<TValue, IObservable<IValidationState>?> stateStream) =>
        read.HasOwner || !SelectedOwnerComparer<TValue>.IsOwnerSelection
            ? stateStream(read.Value) ?? Observable.Return(ValidationState.Valid)
            : Observable.Return(ValidationState.Valid);

    /// <summary>Represents missing state ownership without retaining an old inner subscription.</summary>
    /// <param name="suppress">Whether presentation retains its last value.</param>
    /// <returns>An empty inner stream when suppressed, otherwise the normal valid state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IValidationState> MissingState(bool suppress) =>
        suppress ? Observable.Empty<IValidationState>() : Observable.Return(ValidationState.Valid);

    /// <summary>Represents missing property ownership without retaining old rule subscriptions.</summary>
    /// <param name="suppress">Whether presentation retains its last collection.</param>
    /// <returns>An empty inner stream when suppressed, otherwise the normal empty collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IList<IValidationState>> MissingProperties(bool suppress) =>
        suppress ? Observable.Empty<IList<IValidationState>>() : Observable.Return<IList<IValidationState>>([]);

    /// <summary>Resolves the explicit source provider before its attached registry.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="owner">The current provider owner.</param>
    /// <param name="request">The closed dispatch request.</param>
    /// <param name="expression">The current invocation expression.</param>
    /// <param name="selector">The descriptor, or null when no provider supports it.</param>
    /// <returns>Whether a descriptor was selected.</returns>
    /// <remarks>Implicit model and default-context lookups prefer the expression domain before the delegate domain at each provider or attachment stage.</remarks>
    private static bool TryResolveSelectorOn<TSource, TValue>(
        object? owner,
        ValidationPlanRequest request,
        Expression<Func<TSource, TValue>>? expression,
        out ValidationSelector<TSource, TValue>? selector)
    {
        if (owner is IValidationPlanProvider provider && provider.TryGetSelector(request, expression, out selector))
        {
            return true;
        }

        var implicitSelection = IsImplicitSelector(expression, request);
        if (implicitSelection && TryResolveImplicitDelegateSelectorProvider(owner, request, out selector))
        {
            return true;
        }

        var attached = owner is null ? null : ValidationPlanRegistry.TryGetAttached(owner);
        if (attached is not null && attached.TryGetSelector(request, expression, out selector))
        {
            return true;
        }

        if (implicitSelection && attached is not null)
        {
            return attached.TryGetDelegateSelector(request, null, out selector);
        }

        selector = null;
        return false;
    }

    /// <summary>Recognizes the selector-free roles shared by the expression and delegate domains.</summary>
    /// <typeparam name="TSource">The original selected source type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="expression">The original query, or null for an implicit selection.</param>
    /// <param name="request">The exact selector role.</param>
    /// <returns>Whether both domains may resolve this implicit selection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsImplicitSelector<TSource, TValue>(Expression<Func<TSource, TValue>>? expression, ValidationPlanRequest request) =>
        expression is null && request.Role is ValidationPlanRole.Model or ValidationPlanRole.DefaultContext;

    /// <summary>Queries only the current custom delegate provider for an implicit selection.</summary>
    /// <typeparam name="TSource">The original selected source type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="owner">The current provider owner.</param>
    /// <param name="request">The implicit model or default-context request.</param>
    /// <param name="selector">The successfully selected descriptor, or null for an unsupported query.</param>
    /// <returns>Whether the provider reported an authoritative match.</returns>
    private static bool TryResolveImplicitDelegateSelectorProvider<TSource, TValue>(
        object? owner,
        ValidationPlanRequest request,
        out ValidationSelector<TSource, TValue>? selector)
    {
        if (owner is IValidationDelegatePlanProvider provider && provider.TryGetDelegateSelector(request, null, out selector))
        {
            return true;
        }

        selector = null;
        return false;
    }

    /// <summary>Observes the known typed default context and replacement notifications.</summary>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <param name="model">The model contract.</param>
    /// <param name="owner">The current typed view or host.</param>
    /// <returns>The current default context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<ValidationOwnerRead<IValidationContext?>> ObserveDefaultContext<TModel>(TModel model, object owner)
        where TModel : class, IValidatableViewModel =>
        ObserveOwnership(DefaultContext<TModel>(owner).Bind(model), ReferenceModelComparer<IValidationContext>.Instance);

    /// <summary>Creates an actionable missing-capability error.</summary>
    /// <param name="role">The role contract.</param>
    /// <returns>The dispatch error.</returns>
    private static InvalidOperationException MissingPlan(ValidationPlanRole role) =>
        new($"No typed validation capability is registered for {role}. "
            + "Use the Runic Validation generator, supply an IValidationPlanProvider or IValidationDelegatePlanProvider for the selected API, "
            + "or attach a ValidationPlanRegistry with a matching descriptor. "
            + "Explicit Unsafe APIs remain available for reflection-based execution.");

    /// <summary>Requires one exact property identity for property binding.</summary>
    /// <param name="paths">The paths contract.</param>
    /// <returns>The selected identity.</returns>
    /// <exception cref="InvalidOperationException">The metadata does not select exactly one property.</exception>
    private static ValidationPath SinglePath(IReadOnlyList<ValidationPath> paths) => paths.Count == 1
        ? paths[0]
        : throw new InvalidOperationException("A property binding requires exactly one registered structural property identity.");

    /// <summary>Rejects unknown presentation compatibility flags.</summary>
    /// <param name="initial">The initial contract.</param>
    /// <exception cref="ArgumentOutOfRangeException">The policy includes unknown compatibility flags.</exception>
    private static void ValidateInitial(ValidationInitialSequence initial)
    {
        if ((initial & ~(ValidationInitialSequence.LegacyEmpty | ValidationInitialSequence.LegacyValid)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initial));
        }
    }

    /// <summary>Applies explicit presentation seeds to a borrowed state stream.</summary>
    /// <param name="states">The states contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>The presentation stream.</returns>
    private static IObservable<IValidationState> PreludeState(IObservable<IValidationState> states, ValidationInitialSequence initial) =>
        (initial & (ValidationInitialSequence.LegacyEmpty | ValidationInitialSequence.LegacyValid)) == 0
            ? states
            : states.StartWith(ValidationState.Valid);

    /// <summary>Observes membership and structural metadata for every current component.</summary>
    /// <param name="context">The context contract.</param>
    /// <param name="path">The path contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>Complete matching states.</returns>
    private static IObservable<IList<IValidationState>> ObserveMatchingRules(
        IValidationContext context,
        ValidationPath path,
        bool strict,
        ValidationInitialSequence initial)
    {
        var values = context.Validations.Connect().ToCollection()
            .StartWith(new List<IValidationComponent>(context.Validations.Items))
            .DistinctUntilChanged(RuleMembershipComparer.Instance)
            .Select(rules => ObserveRulePaths(rules)
                .Select(_ => CombineRules(rules, path, strict, initial)).SwitchTo())
            .SwitchTo();

        // The cast selects one empty collection seed rather than an empty sequence of seeds.
        return (initial & ValidationInitialSequence.LegacyEmpty) == 0 ? values : values.StartWith((IList<IValidationState>)[]);
    }

    /// <summary>Observes metadata changes even from components that do not currently match.</summary>
    /// <param name="rules">The rules contract.</param>
    /// <returns>Synchronous metadata invalidations.</returns>
    private static IObservable<Unit> ObserveRulePaths(IEnumerable<IValidationComponent> rules)
    {
        List<IObservable<Unit>> changes = [];
        foreach (var rule in rules)
        {
            if (rule is IValidationPathComponent typed)
            {
                changes.Add(typed.ValidationPathsChanged
                    .StartWith(typed.ValidationPaths)
                    .DistinctUntilChanged(PathListComparer.Instance)
                    .Skip(1)
                    .Select(static _ => Unit.Default));
            }
        }

        return changes.Count == 0 ? Observable.Return(Unit.Default) : changes.Merge().StartWith(Unit.Default);
    }

    /// <summary>Combines complete matching states with an optional legacy membership seed.</summary>
    /// <param name="rules">The rules contract.</param>
    /// <param name="path">The path contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>The combined presentation collection.</returns>
    private static IObservable<IList<IValidationState>> CombineRules(
        IEnumerable<IValidationComponent> rules,
        ValidationPath path,
        bool strict,
        ValidationInitialSequence initial)
    {
        List<IObservable<IValidationState>> streams = [];
        foreach (var rule in rules)
        {
            if (Matches(rule, path, strict))
            {
                streams.Add(rule.ValidationStatusChange.Where(_ => Matches(rule, path, strict)));
            }
        }

        var legacyValid = (initial & ValidationInitialSequence.LegacyValid) != 0;
        var empty = legacyValid ? Observable.Empty<IList<IValidationState>>() : Observable.Return<IList<IValidationState>>([]);
        var combined = streams.Count == 0 ? empty : streams.CombineLatest();
        return legacyValid ? combined.StartWith<IList<IValidationState>>([ValidationState.Valid]) : combined;
    }

    /// <summary>Matches exact structural identities, with ordinal full-name compatibility for explicitly legacy metadata.</summary>
    /// <param name="rule">The rule contract.</param>
    /// <param name="path">The path contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <returns>Whether the current metadata matches.</returns>
    private static bool Matches(IValidationComponent rule, ValidationPath path, bool strict)
    {
        if (rule is not IPropertyValidationComponent legacy)
        {
            return false;
        }

        if (path.IsLegacy || rule is not IValidationPathComponent typed)
        {
            return legacy.ContainsPropertyName(path.DisplayPath, strict);
        }

        if (typed.ContainsPath(path, strict))
        {
            return true;
        }

        var paths = typed.ValidationPaths;
        if (strict && paths.Count != 1)
        {
            return false;
        }

        foreach (var current in paths)
        {
            if (current.IsLegacy && current.Matches(path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Uses borrowed context reference identity and exact path identity.</summary>
    private sealed class ContextPathComparer : IEqualityComparer<(IValidationContext? Context, ValidationPath Path, bool SuppressMissing)>
    {
        /// <summary>Gets the shared stateless comparer.</summary>
        public static ContextPathComparer Instance { get; } = new();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals((IValidationContext? Context, ValidationPath Path, bool SuppressMissing) x, (IValidationContext? Context, ValidationPath Path, bool SuppressMissing) y) =>
            ReferenceEquals(x.Context, y.Context) && Equals(x.Path, y.Path) && x.SuppressMissing == y.SuppressMissing;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode((IValidationContext? Context, ValidationPath Path, bool SuppressMissing) obj) =>
            HashCode.Combine(obj.Path, obj.SuppressMissing);
    }

    /// <summary>Compares complete ordered metadata snapshots.</summary>
    private sealed class PathListComparer : IEqualityComparer<IReadOnlyList<ValidationPath>>
    {
        /// <summary>Gets the shared stateless comparer.</summary>
        public static PathListComparer Instance { get; } = new();

        /// <inheritdoc/>
        public bool Equals(IReadOnlyList<ValidationPath>? x, IReadOnlyList<ValidationPath>? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.Count != y.Count)
            {
                return false;
            }

            for (var index = 0; index < x.Count; index++)
            {
                if (!x[index].Equals(y[index])
                    || !string.Equals(x[index].DisplayPath, y[index].DisplayPath, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(IReadOnlyList<ValidationPath> obj) => obj.Count;
    }

    /// <summary>Compares ordered rule membership by borrowed reference identity.</summary>
    private sealed class RuleMembershipComparer : IEqualityComparer<IReadOnlyCollection<IValidationComponent>>
    {
        /// <summary>Gets the shared stateless comparer.</summary>
        public static RuleMembershipComparer Instance { get; } = new();

        /// <inheritdoc/>
        public bool Equals(IReadOnlyCollection<IValidationComponent>? x, IReadOnlyCollection<IValidationComponent>? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.Count != y.Count)
            {
                return false;
            }

            using var left = x.GetEnumerator();
            using var right = y.GetEnumerator();
            while (left.MoveNext() && right.MoveNext())
            {
                if (!ReferenceEquals(left.Current, right.Current))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(IReadOnlyCollection<IValidationComponent> obj) => obj.Count;
    }

    /// <summary>Uses reference identity for known selected owners and declared equality for ordinary values.</summary>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="values">The declared value comparer.</param>
    private sealed class SelectedOwnerComparer<TValue>(IEqualityComparer<TValue> values) : IEqualityComparer<TValue>
    {
        /// <summary>Gets whether the static selected type owns a helper, context or state-stream subscription.</summary>
        internal static bool IsOwnerSelection { get; } =
            typeof(ValidationHelper).IsAssignableFrom(typeof(TValue))
            || typeof(IValidationContext).IsAssignableFrom(typeof(TValue))
            || typeof(IObservable<IValidationState>).IsAssignableFrom(typeof(TValue));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TValue? x, TValue? y) =>
            !typeof(TValue).IsValueType && IsOwnerSelection
                ? ReferenceEquals(x, y)
                : values.Equals(x, y);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(TValue obj) => !typeof(TValue).IsValueType && IsOwnerSelection
            ? RuntimeHelpers.GetHashCode(obj!)
            : values.GetHashCode(obj!);
    }

    /// <summary>Preserves source replacement even when owners override value equality.</summary>
    /// <typeparam name="TModel">The borrowed reference type.</typeparam>
    private sealed class ReferenceModelComparer<TModel> : IEqualityComparer<TModel?>
        where TModel : class
    {
        /// <summary>Gets the shared stateless comparer.</summary>
        public static ReferenceModelComparer<TModel> Instance { get; } = new();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TModel? x, TModel? y) => ReferenceEquals(x, y);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(TModel? obj) => obj is null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}
