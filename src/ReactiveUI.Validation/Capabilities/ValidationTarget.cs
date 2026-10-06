// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers legal target factories for source types inaccessible outside their defining scope.</summary>
public static class ValidationTarget
{
    /// <summary>Creates a target without retaining its inference witness.</summary>
    /// <typeparam name="TSource">The inferred source type.</typeparam>
    /// <typeparam name="TOut">The assigned value type.</typeparam>
    /// <param name="sourceWitness">Supplies the original source type without being retained.</param>
    /// <param name="bind">Creates typed current assignment plans.</param>
    /// <returns>The reusable target.</returns>
    public static ValidationTarget<TSource, TOut> Create<TSource, TOut>(TSource sourceWitness, Func<TSource, ValidationWritePlan<TOut>> bind)
    {
        _ = sourceWitness;
        return new(bind);
    }
}
