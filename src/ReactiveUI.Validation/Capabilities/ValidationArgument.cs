// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>An identity token for a typed argument in a registered expression schema.</summary>
/// <typeparam name="T">The argument type.</typeparam>
public sealed class ValidationArgument<T>
{
    /// <summary>Stores a typed value with this exact token in a new match payload.</summary>
    /// <param name="arguments">The invocation-local payload.</param>
    /// <param name="value">The current typed argument value.</param>
    /// <returns>The payload carrying this token and value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValidationArguments AddTo(ValidationArguments arguments, T value) => arguments.Add(this, value);
}
