// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Registers caller-created observables without discovering or observing properties.</summary>
/// <remarks>
/// Supply initial states and serialize mutations and notifications on the model owner.
/// Helpers unregister and dispose their rule's subscription, without disposing the caller's observable or context.
/// Failed registration removes any partly admitted rule and disposes its subscription before propagating the original failure.
/// If rollback also fails, an <see cref="AggregateException"/> retains the registration failure as its first inner exception.
/// Dispose helpers before their captured context.
/// </remarks>
[SuppressMessage(
    "Design",
    "SST1703:Use extension block",
    Justification = "SDK 10.0.401 C# 14 static bridges impose incorrect notnull constraints; traditional extensions preserve nullable generic values.")]
public static class ObservableValidationRuleExtensions
{
    /// <summary>Registers complete states in the current default context.</summary>
    /// <param name="viewModel">The context owner captured during registration.</param>
    /// <param name="states">The state stream, including an initial state.</param>
    /// <param name="propertyPaths">Full dot-separated paths, or an empty sequence for a model-wide rule.</param>
    /// <returns>A helper that cleans up the captured context even if the default context changes.</returns>
    /// <exception cref="ArgumentNullException">An argument or path is null.</exception>
    /// <exception cref="ArgumentException">A path is malformed.</exception>
    public static ValidationHelper AddObservableRule(this IValidatableViewModel viewModel, IObservable<IValidationState> states, IEnumerable<string> propertyPaths)
    {
        ArgumentExceptionHelper.ThrowIfNull(viewModel);
        return viewModel.ValidationContext.AddObservableRule(states, propertyPaths);
    }

    /// <summary>Projects supplied values into states in the current default context.</summary>
    /// <typeparam name="TValue">The supplied value type.</typeparam>
    /// <param name="viewModel">The context owner captured during registration.</param>
    /// <param name="values">The value stream, including an initial value.</param>
    /// <param name="validate">Creates a complete state for each value.</param>
    /// <param name="propertyPaths">Full dot-separated paths, or an empty sequence for a model-wide rule.</param>
    /// <returns>A helper owning this rule in the captured default context.</returns>
    /// <exception cref="ArgumentNullException">An argument or path is null.</exception>
    /// <exception cref="ArgumentException">A path is malformed.</exception>
    public static ValidationHelper AddObservableRule<TValue>(
        this IValidatableViewModel viewModel,
        IObservable<TValue> values,
        Func<TValue, IValidationState> validate,
        IEnumerable<string> propertyPaths)
    {
        ArgumentExceptionHelper.ThrowIfNull(viewModel);
        return viewModel.ValidationContext.AddObservableRule(values, validate, propertyPaths);
    }

    /// <summary>Registers complete states with explicit property metadata.</summary>
    /// <param name="context">The context owner captured during registration.</param>
    /// <param name="states">The state stream; custom states pass through unchanged.</param>
    /// <param name="propertyPaths">Full dot-separated paths. Empty means a model-wide rule; duplicates count once.</param>
    /// <returns>A helper that removes and disposes only this rule from its captured context.</returns>
    /// <exception cref="ArgumentNullException">An argument or path is null.</exception>
    /// <exception cref="ArgumentException">A path is empty, has an empty segment, or contains whitespace.</exception>
    public static ValidationHelper AddObservableRule(this IValidationContext context, IObservable<IValidationState> states, IEnumerable<string> propertyPaths)
    {
        ArgumentExceptionHelper.ThrowIfNull(context);
        return ValidationRuleContextExtensions.RegisterValidation(context, new ObservablePropertyValidation(states, propertyPaths));
    }

    /// <summary>Projects caller-created values into complete validation states.</summary>
    /// <typeparam name="TValue">The supplied value type.</typeparam>
    /// <param name="context">The context owner captured during registration.</param>
    /// <param name="values">The value stream, including an initial value.</param>
    /// <param name="validate">Creates a state for each value; it is invoked on the notification thread.</param>
    /// <param name="propertyPaths">Full dot-separated paths, or an empty sequence for a model-wide rule.</param>
    /// <returns>A helper owning this rule in the captured context.</returns>
    /// <exception cref="ArgumentNullException">An argument or path is null.</exception>
    /// <exception cref="ArgumentException">A path is malformed.</exception>
    public static ValidationHelper AddObservableRule<TValue>(
        this IValidationContext context,
        IObservable<TValue> values,
        Func<TValue, IValidationState> validate,
        IEnumerable<string> propertyPaths)
    {
        ArgumentExceptionHelper.ThrowIfNull(values);
        ArgumentExceptionHelper.ThrowIfNull(validate);
        return context.AddObservableRule(values.Select(validate), propertyPaths);
    }
}
