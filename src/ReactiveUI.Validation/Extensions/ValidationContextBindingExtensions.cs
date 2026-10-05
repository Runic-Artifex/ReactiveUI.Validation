// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Binds validation from an explicitly selected, observable context property.</summary>
/// <remarks>
/// Bindings follow view model and context replacement. Null selections clear text, emit an empty property-rule
/// list and emit a valid aggregate state. Bindings own subscriptions, not contexts or rules.
/// </remarks>
[SuppressMessage(
    "Design",
    "SST2309:Do not use optional parameters",
    Justification = "Fixed null formatter and exclusive-property defaults keep this new selected-context surface compact; callers may override either.")]
[SuppressMessage("Design", "SST1703:Use extension block", Justification = "TViewModel is inferred from a method argument and also constrains the receiver TView.")]
public static class ValidationContextBindingExtensions
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
    /// <exception cref="ArgumentNullException">Thrown when view or an expression is null.</exception>
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    public static IValidationBinding BindValidationContext<TView, TViewModel>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty,
        Expression<Func<TView, string>> viewProperty,
        IValidationTextFormatter<string>? formatter = null)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var messages = ObserveState(view, contextProperty).Select(state => formatter.Format(state.Text));
        return new ContextBinding(ValidationBinding.BindToView(messages, (IViewFor<TViewModel>)view, viewProperty));
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
    /// <exception cref="ArgumentNullException">Thrown when view or an expression is null.</exception>
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    public static IValidationBinding BindValidationContext<TView, TViewModel, TProperty>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty,
        Expression<Func<TViewModel, TProperty>> viewModelProperty,
        Expression<Func<TView, string>> viewProperty,
        IValidationTextFormatter<string>? formatter = null,
        bool strict = true)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);
        ArgumentExceptionHelper.ThrowIfNull(viewProperty);
        formatter ??= ValidationTextFormatterResolver.Resolve();
        var messages = ObserveProperty(view, contextProperty, viewModelProperty, strict)
            .Select(states => FirstNonEmptyMessage(states, formatter));
        return new ContextBinding(ValidationBinding.BindToView(messages, (IViewFor<TViewModel>)view, viewProperty));
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
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    public static IValidationBinding BindValidationContext<TView, TViewModel>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty,
        Action<IValidationState> action)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(action);
        return new ContextBinding(ObserveState(view, contextProperty).Do(action).Select(static _ => Unit.Default));
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
    /// <exception cref="ArgumentNullException">Thrown when view, an expression or action is null.</exception>
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    public static IValidationBinding BindValidationContext<TView, TViewModel, TProperty>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty,
        Expression<Func<TViewModel, TProperty>> viewModelProperty,
        Action<IList<IValidationState>> action,
        bool strict = true)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);
        ArgumentExceptionHelper.ThrowIfNull(action);
        return new ContextBinding(ObserveProperty(view, contextProperty, viewModelProperty, strict)
            .Do(action).Select(static _ => Unit.Default));
    }

    /// <summary>Observes context selection, switching both model and context property subscriptions.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The current view.</param>
    /// <param name="contextProperty">The context property expression.</param>
    /// <returns>The current context, including null selections.</returns>
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    private static IObservable<IValidationContext?> ObserveContext<TView, TViewModel>(
        TView view,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        ArgumentExceptionHelper.ThrowIfNull(view);
        ArgumentExceptionHelper.ThrowIfNull(contextProperty);
        return ((IViewFor<TViewModel>)view)
            .WhenAnyValueUnsafe(v => v.ViewModel)
            .Select(viewModel => viewModel is null
                ? Observable.Return<IValidationContext?>(null)
                : viewModel.WhenAnyValueUnsafe(contextProperty))
            .SwitchTo();
    }

    /// <summary>Observes the selected aggregate state.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The current view.</param>
    /// <param name="contextProperty">The context property expression.</param>
    /// <returns>The aggregate state, or a valid state for null selections.</returns>
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IValidationState> ObserveState<TView, TViewModel>(
        TView view,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ObserveContext(view, contextProperty)
            .Select(static context => context is null ? Observable.Return(ValidationState.Valid) : context.ValidationStatusChange)
            .SwitchTo();

    /// <summary>Observes property rules in the currently selected context.</summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="view">The current view.</param>
    /// <param name="contextProperty">The context property expression.</param>
    /// <param name="viewModelProperty">The validated property expression.</param>
    /// <param name="strict">Whether to include only rules validating this property exclusively.</param>
    /// <returns>The selected property states.</returns>
    [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<IList<IValidationState>> ObserveProperty<TView, TViewModel, TProperty>(
        TView view,
        Expression<Func<TViewModel, IValidationContext?>> contextProperty,
        Expression<Func<TViewModel, TProperty>> viewModelProperty,
        bool strict)
        where TView : IViewFor<TViewModel>
        where TViewModel : class, IReactiveObject, IValidatableViewModel =>
        ObserveContext(view, contextProperty)
            .Select(context => context is null
                ? Observable.Return<IList<IValidationState>>([])
                : ObserveRules(context, viewModelProperty, strict))
            .SwitchTo();

    /// <summary>Observes matching rules, seeding membership from the context's actual current rules.</summary>
    /// <typeparam name="TViewModel">The model type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="context">The selected context.</param>
    /// <param name="property">The validated property expression.</param>
    /// <param name="strict">Whether to include only rules validating this property exclusively.</param>
    /// <returns>Actual current states, or an empty list when there are no matching rules.</returns>
    private static IObservable<IList<IValidationState>> ObserveRules<TViewModel, TProperty>(
        IValidationContext context,
        Expression<Func<TViewModel, TProperty>> property,
        bool strict)
    {
        var propertyName = property.Body.GetPropertyPath();
        return context.Validations.Connect().ToCollection()
            .StartWith(new List<IValidationComponent>(context.Validations.Items))
            .Select(validations =>
            {
                List<IObservable<IValidationState>> states = [];
                foreach (var validation in validations)
                {
                    if (validation is IPropertyValidationComponent propertyValidation
                        && propertyValidation.ContainsPropertyName(propertyName, strict))
                    {
                        states.Add(validation.ValidationStatusChange);
                    }
                }

                return states.Count == 0
                    ? Observable.Return<IList<IValidationState>>([])
                    : states.CombineLatest();
            })
            .SwitchTo();
    }

    /// <summary>Formats the first nonempty rule message.</summary>
    /// <param name="states">The property rule states.</param>
    /// <param name="formatter">The message formatter.</param>
    /// <returns>The first message, or an empty string.</returns>
    private static string FirstNonEmptyMessage(IList<IValidationState> states, IValidationTextFormatter<string> formatter)
    {
        foreach (var state in states)
        {
            var message = formatter.Format(state.Text);
            if (!string.IsNullOrEmpty(message))
            {
                return message;
            }
        }

        return string.Empty;
    }

    /// <summary>Owns only the selected-context binding subscription.</summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Dispose atomically exchanges the owned subscription and disposes that value.")]
    private sealed class ContextBinding : IValidationBinding
    {
        /// <summary>The subscription, released once even during reentrant disposal.</summary>
        private IDisposable? _subscription;

        /// <summary>Initializes a new instance of the <see cref="ContextBinding"/> class.</summary>
        /// <param name="updates">The binding's update stream.</param>
        public ContextBinding(IObservable<Unit> updates) => _subscription = SubscribeExtensions.Subscribe(updates);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }
}
