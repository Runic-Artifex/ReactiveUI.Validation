// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers reference-owned storage for anonymous or lexically private values.</summary>
public static class ValidationCell
{
    /// <summary>Creates owned storage for an inferred value type.</summary>
    /// <typeparam name="T">The inferred stored value type.</typeparam>
    /// <param name="initial">The initial storage value.</param>
    /// <returns>The stable storage owner.</returns>
    public static ValidationCell<T> Create<T>(T initial) => new(initial);
}
