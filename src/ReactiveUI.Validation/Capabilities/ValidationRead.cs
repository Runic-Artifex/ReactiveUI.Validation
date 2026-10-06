// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers selection snapshots for anonymous and lexically private values.</summary>
public static class ValidationRead
{
    /// <summary>Creates a present typed snapshot.</summary>
    /// <typeparam name="TValue">The inferred selected value type.</typeparam>
    /// <param name="value">The current selected value.</param>
    /// <param name="paths">The current metadata identities.</param>
    /// <returns>The immutable snapshot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationRead<TValue> Present<TValue>(TValue value, IReadOnlyList<ValidationPath> paths) => ValidationRead<TValue>.Present(value, paths);
}
