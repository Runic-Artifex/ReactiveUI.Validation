// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A finite source of typed delegate descriptors; resolution never invokes selectors or acquires subscriptions.</summary>
public interface IValidationDelegatePlanProvider
{
    /// <summary>Resolves a descriptor for the exact source and selected value types.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="request">The operation and delegate role.</param>
    /// <param name="selection">The supplied typed delegate, or null for an implicit selector.</param>
    /// <param name="selector">The resolved descriptor, or null when unsupported.</param>
    /// <returns>Whether exactly one descriptor is supported.</returns>
    bool TryGetDelegateSelector<TSource, TValue>(ValidationPlanRequest request, Func<TSource, TValue>? selection, [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector);

    /// <summary>Resolves a writable descriptor for the exact source and output types.</summary>
    /// <typeparam name="TSource">The original target source API type.</typeparam>
    /// <typeparam name="TValue">The delegate's selected property type.</typeparam>
    /// <typeparam name="TOut">The assigned output type.</typeparam>
    /// <param name="request">The operation and delegate role.</param>
    /// <param name="selection">The supplied typed delegate; null produces no target match.</param>
    /// <param name="target">The resolved descriptor, or null when unsupported.</param>
    /// <returns>Whether exactly one descriptor is supported.</returns>
    bool TryGetDelegateTarget<TSource, TValue, TOut>(ValidationPlanRequest request, Func<TSource, TValue>? selection, [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target);
}
