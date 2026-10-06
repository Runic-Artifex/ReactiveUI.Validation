// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Binds a legal target factory to a borrowed source.</summary>
/// <typeparam name="TSource">The original API source type.</typeparam>
/// <typeparam name="TOut">The assigned value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationTarget")]
public sealed class ValidationTarget<TSource, TOut>
{
    /// <summary>The bound _bind contract.</summary>
    private readonly Func<TSource, ValidationWritePlan<TOut>> _bind;

    /// <summary>Initializes a new instance of the <see cref="ValidationTarget{TSource,TOut}"/> class.</summary>
    /// <param name="bind">Creates current target access and dependency adapters in their legal lexical context.</param>
    public ValidationTarget(Func<TSource, ValidationWritePlan<TOut>> bind)
    {
        ArgumentExceptionHelper.ThrowIfNull(bind);
        _bind = bind;
    }

    /// <summary>Creates a bound target without subscribing.</summary>
    /// <param name="source">The borrowed root or stable storage handle.</param>
    /// <returns>The bound write plan.</returns>
    /// <exception cref="InvalidOperationException">The factory returns no plan.</exception>
    public ValidationWritePlan<TOut> Bind(TSource source) => _bind(source) ?? throw new InvalidOperationException("The target factory returned no write plan.");
}
