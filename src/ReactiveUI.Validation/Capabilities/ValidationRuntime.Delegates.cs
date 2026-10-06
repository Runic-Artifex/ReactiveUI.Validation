// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Executes registered typed delegate capabilities without invoking the supplied selections during resolution.</summary>
public static partial class ValidationRuntime
{
    /// <summary>Resolves an explicitly registered selector for a typed delegate API.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The source that supplies or owns the provider.</param>
    /// <param name="selection">The current selection, including current captured arguments.</param>
    /// <param name="role">The selector's purpose.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>The typed descriptor.</returns>
    /// <exception cref="InvalidOperationException">No matching typed capability is registered.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [OverloadResolutionPriority(-1)]
    public static ValidationSelector<TSource, TValue> ResolveSelector<TSource, TValue>(
        TSource source,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity) => ResolveSelectorCore(source, source, selection, role, operationIdentity);

    /// <summary>Resolves a current source capability with an explicitly selected fallback provider owner.</summary>
    /// <typeparam name="TSource">The original selected source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current selected source.</param>
    /// <param name="providerOwner">The fallback provider or registry owner, such as the view.</param>
    /// <param name="selection">The current invocation selection.</param>
    /// <param name="role">The selection's purpose.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>The exact typed descriptor, preferring the source's current provider.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [OverloadResolutionPriority(-1)]
    public static ValidationSelector<TSource, TValue> ResolveSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity) => ResolveSelectorCore(source, providerOwner, selection, role, operationIdentity);

    /// <summary>Prefers an explicitly registered selector and otherwise uses a compiled typed descriptor.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current receiver supplied to the selected descriptor.</param>
    /// <param name="providerOwner">The fallback provider owner, such as the view.</param>
    /// <param name="selection">The current invocation selection.</param>
    /// <param name="role">The exact selector role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="generatedFallback">The cold compiled descriptor used only when lookup finds no match.</param>
    /// <returns>The registered descriptor or the supplied compiled descriptor.</returns>
    /// <remarks>Provider and catalog failures, ambiguity and successful null results propagate. They never select the fallback.</remarks>
    [OverloadResolutionPriority(-1)]
    public static ValidationSelector<TSource, TValue> ResolveSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity,
        ValidationSelector<TSource, TValue> generatedFallback)
    {
        ArgumentExceptionHelper.ThrowIfNull(generatedFallback);
        return TryResolveDelegateSelectorCore(source, providerOwner, selection, role, operationIdentity, out var selected)
            ? selected ?? throw MissingPlan(role)
            : generatedFallback;
    }

    /// <summary>Resolves a structural property identity without evaluating the selection's selected value.</summary>
    /// <typeparam name="TSource">The original selected source API type.</typeparam>
    /// <typeparam name="TValue">The selection's original value type.</typeparam>
    /// <param name="source">The current selected source.</param>
    /// <param name="providerOwner">The fallback provider or registry owner.</param>
    /// <param name="selection">The current original property selection.</param>
    /// <param name="role">The selection's registered metadata role.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>A descriptor observing exactly one explicit property identity with the same typed dependencies.</returns>
    /// <exception cref="InvalidOperationException">The bound plan has no metadata reader or does not select exactly one path.</exception>
    [OverloadResolutionPriority(-1)]
    public static ValidationSelector<TSource, ValidationPath> ResolvePathSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity)
    {
        var selector = ResolveSelectorCore(source, providerOwner, selection, role, operationIdentity);
        return new(current => selector.Bind(current).CreatePathPlan());
    }

    /// <summary>Prefers registered metadata and otherwise uses a compiled metadata-only descriptor.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The original selection's selected member type.</typeparam>
    /// <param name="source">The current receiver.</param>
    /// <param name="providerOwner">The fallback provider owner.</param>
    /// <param name="selection">The current property selection.</param>
    /// <param name="role">The exact metadata role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="generatedPathFallback">The cold compiled path descriptor, which never reads the selected leaf.</param>
    /// <returns>The registered metadata descriptor or the supplied compiled path descriptor.</returns>
    /// <remarks>Matched descriptor failures, including missing metadata readers, propagate rather than selecting fallback metadata.</remarks>
    [OverloadResolutionPriority(-1)]
    public static ValidationSelector<TSource, ValidationPath> ResolvePathSelector<TSource, TValue>(
        TSource source,
        object? providerOwner,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity,
        ValidationSelector<TSource, ValidationPath> generatedPathFallback)
    {
        ArgumentExceptionHelper.ThrowIfNull(generatedPathFallback);
        if (!TryResolveDelegateSelectorCore(source, providerOwner, selection, role, operationIdentity, out var selected))
        {
            return generatedPathFallback;
        }

        var selector = selected ?? throw MissingPlan(role);
        return new(current => selector.Bind(current).CreatePathPlan());
    }

    /// <summary>Resolves an explicitly registered writable target, including a caller-supplied conversion.</summary>
    /// <typeparam name="TSource">The original target source API type.</typeparam>
    /// <typeparam name="TValue">The target selection's value type.</typeparam>
    /// <typeparam name="TOut">The actual presentation output type.</typeparam>
    /// <param name="source">The source that supplies or owns the provider.</param>
    /// <param name="selection">The current target selection.</param>
    /// <param name="operationIdentity">The registered operation identity; empty selects compatibility patterns.</param>
    /// <returns>The typed writable descriptor.</returns>
    /// <exception cref="InvalidOperationException">No matching typed capability is registered.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage(
        "Design",
        "SST2307:Type parameter is not inferable",
        Justification = "Output may differ from the selection value type; explicit closed output types select the registered conversion contract.")]
    [OverloadResolutionPriority(-1)]
    public static ValidationTarget<TSource, TOut> ResolveTarget<TSource, TValue, TOut>(
        TSource source,
        Func<TSource, TValue>? selection,
        string operationIdentity) =>
        TryResolveDelegateTargetCore(source, selection, operationIdentity, out ValidationTarget<TSource, TOut>? target)
            ? target ?? throw MissingPlan(ValidationPlanRole.Target)
            : throw MissingPlan(ValidationPlanRole.Target);

    /// <summary>Prefers an explicitly registered target and otherwise uses a compiled typed write descriptor.</summary>
    /// <typeparam name="TSource">The original target source API type.</typeparam>
    /// <typeparam name="TValue">The target selection's original member type.</typeparam>
    /// <typeparam name="TOut">The presentation output type.</typeparam>
    /// <param name="source">The current target receiver.</param>
    /// <param name="selection">The current target selection.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="generatedFallback">The cold compiled target used only when lookup finds no match.</param>
    /// <returns>The registered write descriptor or the supplied compiled descriptor.</returns>
    /// <remarks>Provider and catalog failures, ambiguity and successful null results propagate. They never select the fallback.</remarks>
    [OverloadResolutionPriority(-1)]
    public static ValidationTarget<TSource, TOut> ResolveTarget<TSource, TValue, TOut>(
        TSource source,
        Func<TSource, TValue>? selection,
        string operationIdentity,
        ValidationTarget<TSource, TOut> generatedFallback)
    {
        ArgumentExceptionHelper.ThrowIfNull(generatedFallback);
        return TryResolveDelegateTargetCore(source, selection, operationIdentity, out ValidationTarget<TSource, TOut>? target)
            ? target ?? throw MissingPlan(ValidationPlanRole.Target)
            : generatedFallback;
    }

    /// <summary>Resolves against the current selected source, then its owning view.</summary>
    /// <typeparam name="TSource">The original source type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="source">The current selected source.</param>
    /// <param name="owner">The fallback registry owner.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="role">The selection role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <returns>The exact typed descriptor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValidationSelector<TSource, TValue> ResolveSelectorCore<TSource, TValue>(
        TSource source,
        object? owner,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity) =>
        TryResolveDelegateSelectorCore(source, owner, selection, role, operationIdentity, out var selector)
            ? selector ?? throw MissingPlan(role)
            : throw MissingPlan(role);

    /// <summary>Observes the current registered helper and its complete states.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="selection">The selection contract.</param>
    /// <returns>The current helper states.</returns>
    internal static IObservable<IValidationState> ObserveModelHelper<TView, TModel>(
        TView view,
        Func<TModel, ValidationHelper?> selection)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(selection);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingState(model.SuppressMissing)
                : ObserveOwnership(
                        ResolveSelectorCore(model.Value, view, selection, ValidationPlanRole.Helper, string.Empty).Bind(model.Value),
                        ReferenceModelComparer<ValidationHelper>.Instance)
                    .Select(static read => read.Value is null ? MissingState(read.SuppressMissing) : read.Value.ValidationChanged)
                    .SwitchTo())
            .SwitchTo();
    }

    /// <summary>Observes the current registered context selection.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="selection">The selection contract.</param>
    /// <returns>Current contexts, including null.</returns>
    internal static IObservable<IValidationContext?> ObserveModelContext<TView, TModel>(
        TView view,
        Func<TModel, IValidationContext?> selection)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(selection);
        return ObserveModelContextReads(view, selection).Select(static read => read.Value);
    }

    /// <summary>Observes selected context states while retaining missing-owner presentation policy.</summary>
    /// <typeparam name="TView">The original view type.</typeparam>
    /// <typeparam name="TModel">The current model type.</typeparam>
    /// <param name="view">The borrowed view.</param>
    /// <param name="selection">The registered context selection.</param>
    /// <returns>Complete states, with old subscriptions detached during suppressed absence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<IValidationState> ObserveModelContextState<TView, TModel>(
        TView view,
        Func<TModel, IValidationContext?> selection)
        where TView : IViewFor<TModel>
        where TModel : class =>
        ObserveModelContextReads(view, selection)
            .Select(static read => read.Value is null ? MissingState(read.SuppressMissing) : read.Value.ValidationStatusChange)
            .SwitchTo();

    /// <summary>Observes the current registered metadata in the default context.</summary>
    /// <typeparam name="TView">The typed view contract.</typeparam>
    /// <typeparam name="TModel">The typed model contract.</typeparam>
    /// <typeparam name="TValue">The typed value contract.</typeparam>
    /// <param name="view">The view contract.</param>
    /// <param name="selection">The selection contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>Matching complete states.</returns>
    internal static IObservable<IList<IValidationState>> ObserveModelProperty<TView, TModel, TValue>(
        TView view,
        Func<TModel, TValue> selection,
        bool strict,
        ValidationInitialSequence initial)
        where TView : IViewFor<TModel>
        where TModel : class, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(selection);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingProperties(model.SuppressMissing)
                : ObserveDefaultContext(model.Value, view)
                    .CombineLatest(
                        ResolveSelectorCore(model.Value, view, selection, ValidationPlanRole.Property, string.Empty).Bind(model.Value).ObservePaths(),
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
    /// <param name="contextSelection">The contextSelection contract.</param>
    /// <param name="selection">The selection contract.</param>
    /// <param name="strict">The strict contract.</param>
    /// <param name="initial">The initial contract.</param>
    /// <returns>Matching complete states.</returns>
    internal static IObservable<IList<IValidationState>> ObserveContextProperty<TView, TModel, TValue>(
        TView view,
        Func<TModel, IValidationContext?> contextSelection,
        Func<TModel, TValue> selection,
        bool strict,
        ValidationInitialSequence initial)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(contextSelection);
        ArgumentExceptionHelper.ThrowIfNull(selection);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? MissingProperties(model.SuppressMissing)
                : ObserveOwnership(
                        ResolveSelectorCore(model.Value, view, contextSelection, ValidationPlanRole.Context, string.Empty).Bind(model.Value),
                        ReferenceModelComparer<IValidationContext>.Instance)
                    .CombineLatest(
                        ResolveSelectorCore(model.Value, view, selection, ValidationPlanRole.Property, string.Empty).Bind(model.Value).ObservePaths(),
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
    /// <param name="selection">The current invocation selection.</param>
    /// <param name="role">The exact selector role.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="selector">The descriptor supplied by a successful match.</param>
    /// <returns>Whether lookup reported an actual match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryResolveDelegateSelectorCore<TSource, TValue>(
        TSource source,
        object? owner,
        Func<TSource, TValue>? selection,
        ValidationPlanRole role,
        string operationIdentity,
        out ValidationSelector<TSource, TValue>? selector)
    {
        var request = new ValidationPlanRequest(role, operationIdentity);
        return TryResolveDelegateSelectorOn(source, request, selection, out selector)
            || (!ReferenceEquals(source, owner) && TryResolveDelegateSelectorOn(owner, request, selection, out selector));
    }

    /// <summary>Resolves only actual registered target matches, retaining all provider and catalog failure semantics.</summary>
    /// <typeparam name="TSource">The original target source type.</typeparam>
    /// <typeparam name="TValue">The original selection's member type.</typeparam>
    /// <typeparam name="TOut">The selected output type.</typeparam>
    /// <param name="source">The current receiver and provider owner.</param>
    /// <param name="selection">The current target selection.</param>
    /// <param name="operationIdentity">The explicit or compatibility operation identity.</param>
    /// <param name="target">The exact typed descriptor supplied by a successful match.</param>
    /// <returns>Whether lookup reported an actual match.</returns>
    private static bool TryResolveDelegateTargetCore<TSource, TValue, TOut>(
        TSource source,
        Func<TSource, TValue>? selection,
        string operationIdentity,
        out ValidationTarget<TSource, TOut>? target)
    {
        var request = new ValidationPlanRequest(ValidationPlanRole.Target, operationIdentity);
        if (source is IValidationDelegatePlanProvider provider && provider.TryGetDelegateTarget(request, selection, out target))
        {
            return true;
        }

        var attached = source is null ? null : ValidationPlanRegistry.TryGetAttached(source);
        if (attached is not null && attached.TryGetDelegateTarget(request, selection, out target))
        {
            return true;
        }

        target = null;
        return false;
    }

    /// <summary>Observes selected contexts through each real current model.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="view">The borrowed view.</param>
    /// <param name="selection">The current context selection.</param>
    /// <returns>Context ownership frames that detach old contexts on every missing selection.</returns>
    private static IObservable<ValidationOwnerRead<IValidationContext?>> ObserveModelContextReads<TView, TModel>(
        TView view,
        Func<TModel, IValidationContext?> selection)
        where TView : IViewFor<TModel>
        where TModel : class
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(selection);
        return ObserveModelReads<TView, TModel>(view)
            .Select(model => model.Value is null
                ? Observable.Return(new ValidationOwnerRead<IValidationContext?>(model.HasOwner, null, model.SuppressMissing))
                : ObserveOwnership(
                    ResolveSelectorCore(model.Value, view, selection, ValidationPlanRole.Context, string.Empty).Bind(model.Value),
                    ReferenceModelComparer<IValidationContext>.Instance))
            .SwitchTo();
    }

    /// <summary>Resolves the explicit source provider before its attached registry.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="owner">The current provider owner.</param>
    /// <param name="request">The closed dispatch request.</param>
    /// <param name="selection">The current invocation selection.</param>
    /// <param name="selector">The descriptor, or null when no provider supports it.</param>
    /// <returns>Whether a descriptor was selected.</returns>
    private static bool TryResolveDelegateSelectorOn<TSource, TValue>(
        object? owner,
        ValidationPlanRequest request,
        Func<TSource, TValue>? selection,
        out ValidationSelector<TSource, TValue>? selector)
    {
        if (owner is IValidationDelegatePlanProvider provider && provider.TryGetDelegateSelector(request, selection, out selector))
        {
            return true;
        }

        var attached = owner is null ? null : ValidationPlanRegistry.TryGetAttached(owner);
        if (attached is not null && attached.TryGetDelegateSelector(request, selection, out selector))
        {
            return true;
        }

        selector = null;
        return false;
    }
}
