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

/// <summary>Registers validation rules into an explicitly selected context.</summary>
/// <remarks>These overloads leave the view model's default context unchanged. Dispose each returned helper before disposing its context.</remarks>
public static class ValidationRuleContextExtensions
{
    /// <summary>Provides explicit-context rule extension members for <paramref name="viewModel"/>.</summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="viewModel">The view model whose properties supply validation values.</param>
    extension<TViewModel>(TViewModel viewModel)
        where TViewModel : class, IReactiveObject, IValidatableViewModel
    {
        /// <summary>Setup a validation rule for a specified ViewModel property with static error message.</summary>
        /// <typeparam name="TViewModelProp">ViewModel property type.</typeparam>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="viewModelProperty">ViewModel property.</param>
        /// <param name="isPropertyValid">Func to define if the viewModelProperty is valid or not.</param>
        /// <param name="message">Validation error message.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is empty.</exception>
        [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
        public ValidationHelper ValidationRule<TViewModelProp>(
            IValidationContext context,
            Expression<Func<TViewModel, TViewModelProp?>> viewModelProperty,
            Func<TViewModelProp?, bool> isPropertyValid,
            string message)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);

            ArgumentExceptionHelper.ThrowIfNull(context);

            ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);

            ArgumentExceptionHelper.ThrowIfNull(isPropertyValid);

            ArgumentExceptionHelper.ThrowIfNullOrEmpty(message);

            // We need to associate the ViewModel property with
            // something that can be easily looked up and bound to.
            return RegisterValidation(
                context,
                new BasePropertyValidation<TViewModel, TViewModelProp>(
                    viewModel,
                    viewModelProperty,
                    isPropertyValid,
                    message));
        }

        /// <summary>Setup a validation rule for a specified ViewModel property with dynamic error message.</summary>
        /// <typeparam name="TViewModelProp">ViewModel property type.</typeparam>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="viewModelProperty">ViewModel property.</param>
        /// <param name="isPropertyValid">Func to define if the viewModelProperty is valid or not.</param>
        /// <param name="message">Func to define the validation error message based on the viewModelProperty value.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        [RequiresUnreferencedCode("WhenAnyValue may reference members that could be trimmed in AOT scenarios.")]
        public ValidationHelper ValidationRule<TViewModelProp>(
            IValidationContext context,
            Expression<Func<TViewModel, TViewModelProp?>> viewModelProperty,
            Func<TViewModelProp?, bool> isPropertyValid,
            Func<TViewModelProp?, string> message)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);

            ArgumentExceptionHelper.ThrowIfNull(context);

            ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);

            ArgumentExceptionHelper.ThrowIfNull(isPropertyValid);

            ArgumentExceptionHelper.ThrowIfNull(message);

            return RegisterValidation(
                context,
                new BasePropertyValidation<TViewModel, TViewModelProp>(
                    viewModel,
                    viewModelProperty,
                    isPropertyValid,
                    message));
        }

        /// <summary>Setup a validation rule with a general observable indicating validity and a static error message.</summary>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="validationObservable">Observable to define if the viewModel is valid or not.</param>
        /// <param name="message">Validation error message.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/>, <paramref name="validationObservable"/>, or <paramref name="message"/> is null.</exception>
        /// <remarks>
        /// It should be noted that the observable should provide an initial value, otherwise that can result
        /// in an inconsistent performance.
        /// </remarks>
        public ValidationHelper ValidationRule(
            IValidationContext context,
            IObservable<bool> validationObservable,
            string message)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);

            ArgumentExceptionHelper.ThrowIfNull(context);

            ArgumentExceptionHelper.ThrowIfNull(validationObservable);

            ArgumentExceptionHelper.ThrowIfNull(message);

            return RegisterValidation(
                context,
                new ObservableValidation<TViewModel, bool>(
                    viewModel,
                    validationObservable,
                    static validity => validity,
                    message));
        }

        /// <summary>Setup a validation rule with a general observable based on <see cref="IValidationState"/>.</summary>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="validationObservable">Observable to define if the viewModel is valid or not.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/> or <paramref name="validationObservable"/> is null.</exception>
        /// <remarks>
        /// It should be noted that the observable should provide an initial value, otherwise that can result
        /// in an inconsistent performance.
        /// </remarks>
        public ValidationHelper ValidationRule(
            IValidationContext context,
            IObservable<IValidationState> validationObservable)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);

            ArgumentExceptionHelper.ThrowIfNull(context);

            ArgumentExceptionHelper.ThrowIfNull(validationObservable);

            return RegisterValidation(
                context,
                new ObservableValidation<TViewModel, bool>(
                    validationObservable));
        }

        /// <summary>
        /// Setup a validation rule with a general observable indicating validity and a static error message
        /// for the given view model property.
        /// </summary>
        /// <typeparam name="TViewModelProp">ViewModel property type.</typeparam>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="viewModelProperty">ViewModel property referenced in viewModelObservableProperty.</param>
        /// <param name="viewModelObservable">Observable to define if the viewModel is valid or not.</param>
        /// <param name="message">Validation error message.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>
        /// It should be noted that the observable should provide an initial value, otherwise that can result
        /// in an inconsistent performance.
        /// </remarks>
        public ValidationHelper ValidationRule<TViewModelProp>(
            IValidationContext context,
            Expression<Func<TViewModel, TViewModelProp>> viewModelProperty,
            IObservable<bool> viewModelObservable,
            string message)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);

            ArgumentExceptionHelper.ThrowIfNull(context);

            ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);

            ArgumentExceptionHelper.ThrowIfNull(viewModelObservable);

            ArgumentExceptionHelper.ThrowIfNull(message);

            return RegisterValidation(
                context,
                new ObservableValidation<TViewModel, bool, TViewModelProp>(
                    viewModel,
                    viewModelProperty,
                    viewModelObservable,
                    static validity => validity,
                    message));
        }

        /// <summary>Setup a validation rule with a general observable based on <see cref="IValidationState"/>.</summary>
        /// <typeparam name="TViewModelProp">ViewModel property type.</typeparam>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="viewModelProperty">ViewModel property referenced in viewModelObservableProperty.</param>
        /// <param name="validationObservable">Observable to define if the viewModel is valid or not.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>
        /// It should be noted that the observable should provide an initial value, otherwise that can result
        /// in an inconsistent performance.
        /// </remarks>
        public ValidationHelper ValidationRule<TViewModelProp>(
            IValidationContext context,
            Expression<Func<TViewModel, TViewModelProp>> viewModelProperty,
            IObservable<IValidationState> validationObservable)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);

            ArgumentExceptionHelper.ThrowIfNull(context);

            ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);

            ArgumentExceptionHelper.ThrowIfNull(validationObservable);

            return RegisterValidation(
                context,
                new ObservableValidation<TViewModel, bool, TViewModelProp>(
                    viewModelProperty,
                    validationObservable));
        }

    }

    /// <summary>Registers a rule, capturing its destination for helper cleanup.</summary>
    /// <typeparam name="TValidationComponent">The disposable validation component type.</typeparam>
    /// <param name="context">The destination context.</param>
    /// <param name="validation">The rule to add.</param>
    /// <returns>A helper that removes and disposes this rule, without disposing the context.</returns>
    internal static ValidationHelper RegisterValidation<TValidationComponent>(
        IValidationContext context,
        TValidationComponent validation)
        where TValidationComponent : IValidationComponent, IDisposable
    {
        context.Add(validation);
        return new(validation, Disposable.Create(
            (context, validation),
            static state =>
            {
                try
                {
                    if (!state.context.IsDisposed)
                    {
                        state.context.Remove(state.validation);
                    }
                }
                finally
                {
                    state.validation.Dispose();
                }
            }));
    }
}
