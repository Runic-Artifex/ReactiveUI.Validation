// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.ValidationBindings;
#else
namespace ReactiveUI.Validation.ValidationBindings;
#endif

/// <summary>Provides typed callable selectors for generated or registered validation bindings.</summary>
public sealed partial class ValidationBinding
{
    /// <summary>Creates a binding between a ViewModel property and a view property, using the default formatter.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewModelProperty">ViewModel property type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelProperty">ViewModel property.</param>
    /// <param name="viewProperty">View property.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForProperty<TView, TViewModel, TViewModelProperty, TViewProperty>(
        TView view,
        Func<TViewModel, TViewModelProperty> viewModelProperty,
        Func<TView, TViewProperty> viewProperty)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ForProperty(view, viewModelProperty, viewProperty, null, true);

    /// <summary>Creates a binding between a ViewModel property and a view property.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewModelProperty">ViewModel property type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelProperty">ViewModel property.</param>
    /// <param name="viewProperty">View property.</param>
    /// <param name="formatter">
    /// Validation formatter. Defaults to <see cref="SingleLineFormatter"/> when null. In order to override the global
    /// default value, implement <see cref="IValidationTextFormatter{TOut}"/> and register an instance of
    /// IValidationTextFormatter&lt;string&gt; into Splat.Locator.
    /// </param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/>, <paramref name="viewModelProperty"/>, or <paramref name="viewProperty"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForProperty<TView, TViewModel, TViewModelProperty, TViewProperty>(
        TView view,
        Func<TViewModel, TViewModelProperty> viewModelProperty,
        Func<TView, TViewProperty> viewProperty,
        IValidationTextFormatter<string>? formatter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ForProperty(view, viewModelProperty, viewProperty, formatter, true);

    /// <summary>Creates a binding between a ViewModel property and a view property.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewModelProperty">ViewModel property type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelProperty">ViewModel property.</param>
    /// <param name="viewProperty">View property.</param>
    /// <param name="formatter">
    /// Validation formatter. Defaults to <see cref="SingleLineFormatter"/> when null. In order to override the global
    /// default value, implement <see cref="IValidationTextFormatter{TOut}"/> and register an instance of
    /// IValidationTextFormatter&lt;string&gt; into Splat.Locator.
    /// </param>
    /// <param name="strict">Indicates if the ViewModel property to find is unique.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/>, <paramref name="viewModelProperty"/>, or <paramref name="viewProperty"/> is null.</exception>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForProperty<TView, TViewModel, TViewModelProperty, TViewProperty>(
        TView view,
        Func<TViewModel, TViewModelProperty> viewModelProperty,
        Func<TView, TViewProperty> viewProperty,
        IValidationTextFormatter<string>? formatter,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var target = ValidationRuntime.ResolveTarget<TView, TViewProperty, string>(view, viewProperty, string.Empty);
        var states = ValidationRuntime.ObserveModelProperty(view, viewModelProperty, strict, ValidationInitialSequence.Actual);
        return target.Bind(view).Bind(states.Select(values => FirstNonEmptyMessage(values, formatter)));
    }

    /// <summary>
    /// Creates a binding from a specified ViewModel property to a provided action. Such action binding allows
    /// to easily create new and more specialized platform-specific BindValidation extension methods like those
    /// we have in <see cref="ViewForExtensions" /> targeting the Android platform.
    /// </summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewModelProperty">ViewModel property type.</typeparam>
    /// <typeparam name="TOut">Action return type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelProperty">ViewModel property.</param>
    /// <param name="action">Action to be executed.</param>
    /// <param name="formatter">Validation formatter.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForProperty<TView, TViewModel, TViewModelProperty, TOut>(
        TView view,
        Func<TViewModel, TViewModelProperty> viewModelProperty,
        Action<IList<IValidationState>, IList<TOut>> action,
        IValidationTextFormatter<TOut> formatter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ForProperty(view, viewModelProperty, action, formatter, true);

    /// <summary>
    /// Creates a binding from a specified ViewModel property to a provided action. Such action binding allows
    /// to easily create new and more specialized platform-specific BindValidation extension methods like those
    /// we have in <see cref="ViewForExtensions" /> targeting the Android platform.
    /// </summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewModelProperty">ViewModel property type.</typeparam>
    /// <typeparam name="TOut">Action return type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelProperty">ViewModel property.</param>
    /// <param name="action">Action to be executed.</param>
    /// <param name="formatter">Validation formatter.</param>
    /// <param name="strict">Indicates if the ViewModel property to find is unique.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForProperty<TView, TViewModel, TViewModelProperty, TOut>(
        TView view,
        Func<TViewModel, TViewModelProperty> viewModelProperty,
        Action<IList<IValidationState>, IList<TOut>> action,
        IValidationTextFormatter<TOut> formatter,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);
        ArgumentExceptionHelper.ThrowIfNull(action);
        ArgumentExceptionHelper.ThrowIfNull(formatter);
        var states = ValidationRuntime.ObserveModelProperty(view, viewModelProperty, strict, ValidationInitialSequence.Actual);
        return ValidationRuntime.Bind(states, values => action(values, ValidationRuntime.FormatAll(values, formatter)));
    }

    /// <summary>Creates a binding between a <see cref="ValidationHelper" /> and a specified View property, using the default formatter.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelHelperProperty">ViewModel's ValidationHelper property.</param>
    /// <param name="viewProperty">View property to bind the validation message.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForValidationHelperProperty<TView, TViewModel, TViewProperty>(
        TView view,
        Func<TViewModel?, ValidationHelper?> viewModelHelperProperty,
        Func<TView, TViewProperty> viewProperty)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ForValidationHelperProperty(view, viewModelHelperProperty, viewProperty, null);

    /// <summary>Creates a binding between a <see cref="ValidationHelper" /> and a specified View property.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelHelperProperty">ViewModel's ValidationHelper property.</param>
    /// <param name="viewProperty">View property to bind the validation message.</param>
    /// <param name="formatter">
    /// Validation formatter. Defaults to <see cref="SingleLineFormatter"/> when null. In order to override the global
    /// default value, implement <see cref="IValidationTextFormatter{TOut}"/> and register an instance of
    /// IValidationTextFormatter&lt;string&gt; into Splat.Locator.
    /// </param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/>, <paramref name="viewModelHelperProperty"/>, or <paramref name="viewProperty"/> is null.</exception>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForValidationHelperProperty<TView, TViewModel, TViewProperty>(
        TView view,
        Func<TViewModel?, ValidationHelper?> viewModelHelperProperty,
        Func<TView, TViewProperty> viewProperty,
        IValidationTextFormatter<string>? formatter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(viewModelHelperProperty);
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var target = ValidationRuntime.ResolveTarget<TView, TViewProperty, string>(view, viewProperty, string.Empty);
        var states = ValidationRuntime.ObserveModelHelper<TView, TViewModel>(view, viewModelHelperProperty!);
        return target.Bind(view).Bind(states.Select(state => formatter.Format(state.Text)));
    }

    /// <summary>
    /// Creates a binding from a <see cref="ValidationHelper" /> to a specified action. Such action binding allows
    /// to easily create new and more specialized platform-specific BindValidation extension methods like those
    /// we have in <see cref="ViewForExtensions" /> targeting the Android platform.
    /// </summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TOut">Action return type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewModelHelperProperty">ViewModel's ValidationHelper property.</param>
    /// <param name="action">Action to be executed.</param>
    /// <param name="formatter">Validation formatter.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForValidationHelperProperty<TView, TViewModel, TOut>(
        TView view,
        Func<TViewModel?, ValidationHelper?> viewModelHelperProperty,
        Action<IValidationState, TOut> action,
        IValidationTextFormatter<TOut> formatter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(viewModelHelperProperty);
        ArgumentExceptionHelper.ThrowIfNull(action);
        ArgumentExceptionHelper.ThrowIfNull(formatter);
        var states = ValidationRuntime.ObserveModelHelper<TView, TViewModel>(view, viewModelHelperProperty!);
        return ValidationRuntime.Bind(states, state => action(state, formatter.Format(state.Text)));
    }

    /// <summary>Creates a binding between a ViewModel and a View property, using the default formatter.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewProperty">View property to bind the validation message.</param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/> or <paramref name="viewProperty"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Design", "SST2307:Type parameter is not inferable", Justification = ViewModelTypeNotInferable)]
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForViewModel<TView, TViewModel, TViewProperty>(
        TView view,
        Func<TView, TViewProperty> viewProperty)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ForViewModel<TView, TViewModel, TViewProperty>(view, viewProperty, null);

    /// <summary>Creates a binding between a ViewModel and a View property.</summary>
    /// <typeparam name="TView">ViewFor of ViewModel type.</typeparam>
    /// <typeparam name="TViewModel">ViewModel type.</typeparam>
    /// <typeparam name="TViewProperty">View property type.</typeparam>
    /// <param name="view">View instance.</param>
    /// <param name="viewProperty">View property to bind the validation message.</param>
    /// <param name="formatter">
    /// Validation formatter. Defaults to <see cref="SingleLineFormatter"/> when null. In order to override the global
    /// default value, implement <see cref="IValidationTextFormatter{TOut}"/> and register an instance of
    /// IValidationTextFormatter&lt;string&gt; into Splat.Locator.
    /// </param>
    /// <returns>Returns a validation component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/> or <paramref name="viewProperty"/> is null.</exception>
    [SuppressMessage("Design", "SST2307:Type parameter is not inferable", Justification = ViewModelTypeNotInferable)]
    [OverloadResolutionPriority(1)]
    public static IValidationBinding ForViewModel<TView, TViewModel, TViewProperty>(
        TView view,
        Func<TView, TViewProperty> viewProperty,
        IValidationTextFormatter<string>? formatter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var target = ValidationRuntime.ResolveTarget<TView, TViewProperty, string>(view, viewProperty, string.Empty);
        var states = ValidationRuntime.ObserveModelState<TView, TViewModel>(view);
        return target.Bind(view).Bind(states.Select(state => formatter.Format(state.Text)));
    }
}
