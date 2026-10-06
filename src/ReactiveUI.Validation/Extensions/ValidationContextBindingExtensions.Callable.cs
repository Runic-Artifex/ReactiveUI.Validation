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
public static partial class ValidationContextBindingExtensions
{
    /// <summary>Binds the selected context's aggregate validation text to a string view property.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The view whose current view model is observed.</param>
    /// <param name="viewModel">Used only for type inference.</param>
    /// <param name="contextProperty">The observable context property to select.</param>
    /// <param name="viewProperty">The string view property to update.</param>
    /// <param name="formatter">The text formatter, or null for the registered default.</param>
    /// <returns>A binding that detaches its subscriptions when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when view or a selector is null.</exception>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationContextUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationContext<TView, TViewModel>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, IValidationContext?> contextProperty,
        Func<TView, string> viewProperty,
        IValidationTextFormatter<string>? formatter = null)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var target = ValidationRuntime.ResolveTarget<TView, string, string>(view, viewProperty, string.Empty);
        var states = ValidationRuntime.ObserveModelContextState(view, contextProperty);
        return target.Bind(view).Bind(states.Select(state => formatter.Format(state.Text)));
    }

    /// <summary>Binds the selected context's validation text for a property to a string view property.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <typeparam name="TProperty">The view model property type.</typeparam>
    /// <param name="view">The view whose current view model is observed.</param>
    /// <param name="viewModel">Used only for type inference.</param>
    /// <param name="contextProperty">The observable context property to select.</param>
    /// <param name="viewModelProperty">The property whose validation text is displayed.</param>
    /// <param name="viewProperty">The string view property to update.</param>
    /// <param name="formatter">The text formatter, or null for the registered default.</param>
    /// <param name="strict">Whether to include only rules validating this property exclusively.</param>
    /// <returns>A binding that detaches its subscriptions when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when view or a selector is null.</exception>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationContextUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationContext<TView, TViewModel, TProperty>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, IValidationContext?> contextProperty,
        Func<TViewModel, TProperty> viewModelProperty,
        Func<TView, string> viewProperty,
        IValidationTextFormatter<string>? formatter = null,
        bool strict = true)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var target = ValidationRuntime.ResolveTarget<TView, string, string>(view, viewProperty, string.Empty);
        var states = ValidationRuntime.ObserveContextProperty(view, contextProperty, viewModelProperty, strict, ValidationInitialSequence.Actual);
        return target.Bind(view).Bind(states.Select(current => FirstNonEmptyMessage(current, formatter)));
    }

    /// <summary>Sends the selected context's aggregate state to an action.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The view whose current view model is observed.</param>
    /// <param name="viewModel">Used only for type inference.</param>
    /// <param name="contextProperty">The observable context property to select.</param>
    /// <param name="action">Receives aggregate validity and text, including a valid state while the selection is null.</param>
    /// <returns>A binding that detaches its subscriptions when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when view, contextProperty or action is null.</exception>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationContextUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationContext<TView, TViewModel>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, IValidationContext?> contextProperty,
        Action<IValidationState> action)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(action);
        var states = ValidationRuntime.ObserveModelContextState(view, contextProperty);
        return ValidationRuntime.Bind(states, action);
    }

    /// <summary>Sends the selected context's states for a property to an action.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <typeparam name="TProperty">The view model property type.</typeparam>
    /// <param name="view">The view whose current view model is observed.</param>
    /// <param name="viewModel">Used only for type inference.</param>
    /// <param name="contextProperty">The observable context property to select.</param>
    /// <param name="viewModelProperty">The property whose rules are observed.</param>
    /// <param name="action">Receives actual matching rule states; a null context or no matching rules emits an empty list.</param>
    /// <param name="strict">Whether to include only rules validating this property exclusively.</param>
    /// <returns>A binding that detaches its subscriptions when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when view, a selector or action is null.</exception>
    /// <remarks>Uses the Runic Validation generator or an explicitly registered typed capability. Use BindValidationContextUnsafe for reflection-based execution.</remarks>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding BindValidationContext<TView, TViewModel, TProperty>(
        this TView view,
        TViewModel? viewModel,
        Func<TViewModel, IValidationContext?> contextProperty,
        Func<TViewModel, TProperty> viewModelProperty,
        Action<IList<IValidationState>> action,
        bool strict = true)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(action);
        return ValidationRuntime.Bind(ValidationRuntime.ObserveContextProperty(view, contextProperty, viewModelProperty, strict, ValidationInitialSequence.Actual), action);
    }
}
