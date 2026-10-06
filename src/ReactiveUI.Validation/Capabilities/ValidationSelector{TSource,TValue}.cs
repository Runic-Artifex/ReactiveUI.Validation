// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A typed factory that binds legal access to a caller-owned source.</summary>
/// <typeparam name="TSource">The source's original API type.</typeparam>
/// <typeparam name="TValue">The selected value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationSelector")]
public sealed class ValidationSelector<TSource, TValue>
{
    /// <summary>The bound _bind contract.</summary>
    private readonly Func<TSource, ValidationAccessPlan<TValue>> _bind;

    /// <summary>Initializes a new instance of the <see cref="ValidationSelector{TSource,TValue}"/> class.</summary>
    /// <param name="bind">Creates bound getters and dependencies in their legal lexical context.</param>
    public ValidationSelector(Func<TSource, ValidationAccessPlan<TValue>> bind)
    {
        ArgumentExceptionHelper.ThrowIfNull(bind);
        _bind = bind;
    }

    /// <summary>Binds the descriptor without acquiring any subscription.</summary>
    /// <param name="source">The borrowed source or reference-owned storage handle.</param>
    /// <returns>The bound access plan.</returns>
    /// <exception cref="InvalidOperationException">The factory returns no access plan.</exception>
    public ValidationAccessPlan<TValue> Bind(TSource source) => _bind(source) ?? throw new InvalidOperationException("The selector factory returned no access plan.");
}
