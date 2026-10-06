// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Registers validation rules using generated or explicitly registered typed delegate capabilities.</summary>
public static partial class ValidationRuleContextExtensions
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
        /// <remarks>Uses a generated typed plan or an explicitly registered delegate capability. An unregistered opaque delegate is rejected without invoking its getter.</remarks>
        [OverloadResolutionPriority(1)]
        public ValidationHelper ValidationRule<TViewModelProp>(
            IValidationContext context,
            Func<TViewModel, TViewModelProp?> viewModelProperty,
            Func<TViewModelProp?, bool> isPropertyValid,
            string message)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);
            ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);
            ArgumentExceptionHelper.ThrowIfNull(isPropertyValid);
            ArgumentExceptionHelper.ThrowIfNull(context);
            ArgumentExceptionHelper.ThrowIfNullOrEmpty(message);
            var selector = ValidationRuntime.ResolveSelector(viewModel, viewModelProperty, ValidationPlanRole.RuleValue, string.Empty);
            return ValidationRuntime.RegisterRule(viewModel, context, selector, value =>
            {
                var valid = isPropertyValid(value);
                return new ValidationState(valid, valid ? ValidationText.Empty : ValidationText.Create(message));
            });
        }

        /// <summary>Setup a validation rule for a specified ViewModel property with dynamic error message.</summary>
        /// <typeparam name="TViewModelProp">ViewModel property type.</typeparam>
        /// <param name="context">The destination context, captured until the helper is disposed.</param>
        /// <param name="viewModelProperty">ViewModel property.</param>
        /// <param name="isPropertyValid">Func to define if the viewModelProperty is valid or not.</param>
        /// <param name="message">Func to define the validation error message based on the viewModelProperty value.</param>
        /// <returns>Returns a <see cref="ValidationHelper"/> object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>Uses a generated typed plan or an explicitly registered delegate capability. An unregistered opaque delegate is rejected without invoking its getter.</remarks>
        [OverloadResolutionPriority(1)]
        public ValidationHelper ValidationRule<TViewModelProp>(
            IValidationContext context,
            Func<TViewModel, TViewModelProp?> viewModelProperty,
            Func<TViewModelProp?, bool> isPropertyValid,
            Func<TViewModelProp?, string> message)
        {
            ArgumentExceptionHelper.ThrowIfNull(viewModel);
            ArgumentExceptionHelper.ThrowIfNull(viewModelProperty);
            ArgumentExceptionHelper.ThrowIfNull(isPropertyValid);
            ArgumentExceptionHelper.ThrowIfNull(context);
            ArgumentExceptionHelper.ThrowIfNull(message);
            var selector = ValidationRuntime.ResolveSelector(viewModel, viewModelProperty, ValidationPlanRole.RuleValue, string.Empty);
            return ValidationRuntime.RegisterRule(viewModel, context, selector, value =>
            {
                var valid = isPropertyValid(value);
                return new ValidationState(valid, valid ? ValidationText.None : ValidationText.Create(message(value)));
            });
        }
    }
}
