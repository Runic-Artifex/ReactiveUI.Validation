// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Binds validation states to typed presentation values.</summary>
[SuppressMessage("Design", "SST1703:Use extension block", Justification = "The receiver constraint refers to the method's inferred view model type.")]
public static class ValidationStateBindingExtensions
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
    [RequiresUnreferencedCode("Expression-based observation and property assignment may reference trimmed members.")]
    public static IValidationBinding BindValidationState<TView, TViewModel, TOut>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, ValidationHelper?>> helperProperty,
        Expression<Func<TView, TOut>> viewProperty,
        Func<IValidationState, TOut> converter)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        ArgumentExceptionHelper.ThrowIfNull(converter);
        return ValidationStateBinding.BindToView(ObserveHelper(view, helperProperty).Select(converter), (IViewFor<TViewModel>)view, viewProperty);
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
    [RequiresUnreferencedCode("Expression-based observation may reference trimmed members.")]
    public static IValidationBinding BindValidationState<TView, TViewModel, TOut>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, ValidationHelper?>> helperProperty,
        Func<IValidationState, TOut> converter,
        Action<TOut> onNext)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(onNext);
        return new ValidationStateBinding(ObserveHelper(view, helperProperty).Do(state => onNext(converter(state))).Select(static _ => Unit.Default));
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
    [RequiresUnreferencedCode("Expression-based observation and property assignment may reference trimmed members.")]
    public static IValidationBinding BindValidationState<TView, TViewModel, TProperty, TOut>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, TProperty>> modelProperty,
        Expression<Func<TView, TOut>> viewProperty,
        Func<IList<IValidationState>, TOut> converter,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        ArgumentExceptionHelper.ThrowIfNull(converter);
        return ValidationStateBinding.BindToView(ObserveProperty(view, modelProperty, strict).Select(converter), (IViewFor<TViewModel>)view, viewProperty);
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
    [RequiresUnreferencedCode("Expression-based observation may reference trimmed members.")]
    public static IValidationBinding BindValidationState<TView, TViewModel, TProperty, TOut>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, TProperty>> modelProperty,
        Func<IList<IValidationState>, TOut> converter,
        Action<TOut> onNext,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(onNext);
        return new ValidationStateBinding(ObserveProperty(view, modelProperty, strict).Do(states => onNext(converter(states))).Select(static _ => Unit.Default));
    }

    /// <summary>Observes the current helper, detaching replaced helpers and models.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TViewModel">View model type.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="helperProperty">Helper selector.</param>
    /// <returns>The current helper state or a valid state.</returns>
    [RequiresUnreferencedCode("Expression-based observation may reference trimmed members.")]
    private static IObservable<IValidationState> ObserveHelper<TView, TViewModel>(TView view, Expression<Func<TViewModel, ValidationHelper?>> helperProperty)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(helperProperty);
        return ((IViewFor<TViewModel>)view).WhenAnyValueUnsafe(v => v.ViewModel)
            .Select(vm => vm is null
                ? Observable.Return(ValidationState.Valid)
                : vm.WhenAnyValueUnsafe(helperProperty)
                    .Select(static helper => helper is null ? Observable.Return(ValidationState.Valid) : helper.ValidationChanged).SwitchTo())
            .SwitchTo();
    }

    /// <summary>Observes the current model's matching property rules.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TViewModel">View model type.</typeparam>
    /// <typeparam name="TProperty">Property type.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="modelProperty">Property selector.</param>
    /// <param name="strict">Whether to match exclusive rules.</param>
    /// <returns>All matching current states, or an empty list.</returns>
    [RequiresUnreferencedCode("Expression-based observation may reference trimmed members.")]
    private static IObservable<IList<IValidationState>> ObserveProperty<TView, TViewModel, TProperty>(TView view, Expression<Func<TViewModel, TProperty>> modelProperty, bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(modelProperty);
        var propertyName = modelProperty.Body.GetPropertyPath();
        return ((IViewFor<TViewModel>)view).WhenAnyValueUnsafe(v => v.ViewModel)
            .Select(vm => vm is null
                ? Observable.Return<IList<IValidationState>>(Array.Empty<IValidationState>())
                : vm.ValidationContext.Validations.Connect().ToCollection()
                    .StartWith(new List<IValidationComponent>(vm.ValidationContext.Validations.Items))
                    .Select(rules => ObserveRules(rules, propertyName, strict)).SwitchTo())
            .SwitchTo();
    }

    /// <summary>Combines matching rule states without inserting synthetic states.</summary>
    /// <param name="rules">Current rules.</param>
    /// <param name="propertyName">Validated property path.</param>
    /// <param name="strict">Whether to match exclusive rules.</param>
    /// <returns>The current rule state list.</returns>
    private static IObservable<IList<IValidationState>> ObserveRules(IReadOnlyCollection<IValidationComponent> rules, string propertyName, bool strict)
    {
        List<IObservable<IValidationState>> streams = [];
        foreach (var rule in rules)
        {
            if (rule is IPropertyValidationComponent propertyRule && propertyRule.ContainsPropertyName(propertyName, strict))
            {
                streams.Add(propertyRule.ValidationStatusChange);
            }
        }

        return streams.Count == 0 ? Observable.Return<IList<IValidationState>>(Array.Empty<IValidationState>()) : streams.CombineLatest();
    }
}
