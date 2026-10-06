// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A finite source of typed descriptors; resolution never acquires subscriptions.</summary>
public interface IValidationPlanProvider
{
    /// <summary>Resolves a descriptor for the exact source and selected value types.</summary>
    /// <typeparam name="TSource">The original source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="request">The operation and expression role.</param>
    /// <param name="expression">The original expression, when the operation has one.</param>
    /// <param name="selector">The resolved descriptor, or null when unsupported.</param>
    /// <returns>Whether exactly one descriptor is supported.</returns>
    bool TryGetSelector<TSource, TValue>(ValidationPlanRequest request, Expression<Func<TSource, TValue>>? expression, [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector);

    /// <summary>Resolves a writable descriptor for the exact source and output types.</summary>
    /// <typeparam name="TSource">The original target source API type.</typeparam>
    /// <typeparam name="TValue">The expression's selected property type.</typeparam>
    /// <typeparam name="TOut">The assigned output type.</typeparam>
    /// <param name="request">The operation and expression role.</param>
    /// <param name="expression">The original expression, when the operation has one.</param>
    /// <param name="target">The resolved descriptor, or null when unsupported.</param>
    /// <returns>Whether exactly one descriptor is supported.</returns>
    bool TryGetTarget<TSource, TValue, TOut>(ValidationPlanRequest request, Expression<Func<TSource, TValue>>? expression, [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target);
}
