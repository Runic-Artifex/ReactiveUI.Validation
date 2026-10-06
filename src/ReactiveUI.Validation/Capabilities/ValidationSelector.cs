// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers typed selector factories for anonymous and lexically private source or value types.</summary>
public static class ValidationSelector
{
    /// <summary>Creates a selector without retaining the inference witness.</summary>
    /// <typeparam name="TSource">The inferred original source type.</typeparam>
    /// <typeparam name="TValue">The inferred selected value type.</typeparam>
    /// <param name="sourceWitness">A value supplying the source type; the factory does not retain it.</param>
    /// <param name="bind">Creates legal typed access in its defining scope.</param>
    /// <returns>The reusable selector.</returns>
    public static ValidationSelector<TSource, TValue> Create<TSource, TValue>(TSource sourceWitness, Func<TSource, ValidationAccessPlan<TValue>> bind)
    {
        _ = sourceWitness;
        return new(bind);
    }
}
