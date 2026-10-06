// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers bound write plans from explicit typed assignments.</summary>
public static class ValidationWritePlan
{
    /// <summary>Creates an inferred bound write plan.</summary>
    /// <typeparam name="TOut">The inferred assigned value type.</typeparam>
    /// <param name="resolve">Reads current typed assignment access.</param>
    /// <param name="dependencies">The current target notification contracts.</param>
    /// <returns>The bound write plan.</returns>
    public static ValidationWritePlan<TOut> Create<TOut>(Func<ValidationTargetAccess<TOut>> resolve, IEnumerable<ValidationDependency> dependencies) => new(resolve, dependencies);
}
