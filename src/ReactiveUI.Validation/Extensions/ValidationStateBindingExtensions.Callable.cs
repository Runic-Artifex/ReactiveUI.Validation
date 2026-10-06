// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Provides typed callable selectors for generated or registered validation bindings.</summary>
public static partial class ValidationStateBindingExtensions
{
    /// <summary>Binds a helper's validity and text to a typed view property.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TViewModel">View model type.</typeparam>
    /// <typeparam name="TOut">Presentation value type.</typeparam>
    /// <param name="view">The view whose current ViewModel is observed.</param>
    /// <param name="viewModel">Used only for type inference; may be null.</param>
    /// <param name="helperProperty">The helper property to observe.</param>
    /// <param name="viewProperty">The presentation property to assign.</param>
    /// <param name="converter">Converts the complete state, including validity independent of text.</param>
    /// <returns>A disposable subscription that stops updates.</returns>
    /// <exception cref="ArgumentNullException">Any argument other than <paramref name="viewModel"/> is null.</exception>
    /// <remarks>
    /// A null model or helper produces <see cref="ValidationState.Valid"/>. Assignments run on the source notification thread.
    /// </remarks>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationStateUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationState<TView, TViewModel, TOut>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, ValidationHelper?> helperProperty,
        Func<TView, TOut> viewProperty,
        Func<IValidationState, TOut> converter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        var target = ValidationRuntime.ResolveTarget<TView, TOut, TOut>(view, viewProperty, string.Empty);
        return target.Bind(view).Bind(ValidationRuntime.ObserveModelHelper(view, helperProperty).Select(converter));
    }

    /// <summary>Projects a helper's validity and text into a typed callback value.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TViewModel">View model type.</typeparam>
    /// <typeparam name="TOut">Presentation value type.</typeparam>
    /// <param name="view">The view whose current ViewModel is observed.</param>
    /// <param name="viewModel">Used only for type inference; may be null.</param>
    /// <param name="helperProperty">The helper property to observe.</param>
    /// <param name="converter">Converts the complete state, including validity independent of text.</param>
    /// <param name="onNext">Receives each converted value.</param>
    /// <returns>A disposable subscription that stops updates.</returns>
    /// <exception cref="ArgumentNullException">Any argument other than <paramref name="viewModel"/> is null.</exception>
    /// <remarks>
    /// A null model or helper produces <see cref="ValidationState.Valid"/>. Callbacks run on the source notification thread.
    /// </remarks>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationStateUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationState<TView, TViewModel, TOut>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, ValidationHelper?> helperProperty,
        Func<IValidationState, TOut> converter,
        Action<TOut> onNext)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(onNext);
        return ValidationRuntime.Bind(ValidationRuntime.ObserveModelHelper(view, helperProperty).Select(converter), onNext);
    }

    /// <summary>Binds all matching property rule states to a typed view property.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TViewModel">View model type.</typeparam>
    /// <typeparam name="TProperty">Model property type.</typeparam>
    /// <typeparam name="TOut">Presentation value type.</typeparam>
    /// <param name="view">The view whose current ViewModel is observed.</param>
    /// <param name="viewModel">Used only for type inference; may be null.</param>
    /// <param name="modelProperty">The validated model property.</param>
    /// <param name="viewProperty">The presentation property to assign.</param>
    /// <param name="converter">Converts all matching states; treat the list as read-only.</param>
    /// <param name="strict">If true, includes only rules validating this property exclusively.</param>
    /// <returns>A disposable subscription that stops updates.</returns>
    /// <exception cref="ArgumentNullException">Any argument other than <paramref name="viewModel"/> is null.</exception>
    /// <remarks>
    /// A null model or no matching rules produces an empty list. Active rules must each emit an initial state; no synthetic valid state is inserted. Assignments run on the source notification thread.
    /// </remarks>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationStateUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationState<TView, TViewModel, TProperty, TOut>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, TProperty> modelProperty,
        Func<TView, TOut> viewProperty,
        Func<IList<IValidationState>, TOut> converter,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        var target = ValidationRuntime.ResolveTarget<TView, TOut, TOut>(view, viewProperty, string.Empty);
        return target.Bind(view).Bind(ValidationRuntime.ObserveModelProperty(view, modelProperty, strict, ValidationInitialSequence.Actual).Select(converter));
    }

    /// <summary>Projects all matching property rule states into a typed callback value.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TViewModel">View model type.</typeparam>
    /// <typeparam name="TProperty">Model property type.</typeparam>
    /// <typeparam name="TOut">Presentation value type.</typeparam>
    /// <param name="view">The view whose current ViewModel is observed.</param>
    /// <param name="viewModel">Used only for type inference; may be null.</param>
    /// <param name="modelProperty">The validated model property.</param>
    /// <param name="converter">Converts all matching states; treat the list as read-only.</param>
    /// <param name="onNext">Receives each converted value.</param>
    /// <param name="strict">If true, includes only rules validating this property exclusively.</param>
    /// <returns>A disposable subscription that stops updates.</returns>
    /// <exception cref="ArgumentNullException">Any argument other than <paramref name="viewModel"/> is null.</exception>
    /// <remarks>
    /// A null model or no matching rules produces an empty list. Active rules must each emit an initial state; no synthetic valid state is inserted. Callbacks run on the source notification thread.
    /// </remarks>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationStateUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationState<TView, TViewModel, TProperty, TOut>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, TProperty> modelProperty,
        Func<IList<IValidationState>, TOut> converter,
        Action<TOut> onNext,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(onNext);
        return ValidationRuntime.Bind(ValidationRuntime.ObserveModelProperty(view, modelProperty, strict, ValidationInitialSequence.Actual).Select(converter), onNext);
    }
}
