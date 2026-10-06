// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Creates typed callbacks with only the output type specified; source types are inferred.</summary>
/// <typeparam name="TOutput">The callback output, including nullable reference or value types.</typeparam>
public static class ValidationOutput<TOutput>
{
    /// <summary>Binds projected complete states from the latest source.</summary>
    /// <typeparam name="TSource">The inferred selected source type.</typeparam>
    /// <param name="sources">The source and replacement stream.</param>
    /// <param name="selectStates">The typed state selector; a null selection means valid.</param>
    /// <param name="convert">The complete-state projection.</param>
    /// <param name="apply">The output receiver.</param>
    /// <returns>The owned binding.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IValidationBinding FromStates<TSource>(
        IObservable<TSource?> sources,
        Func<TSource, IObservable<IValidationState>?> selectStates,
        Func<IValidationState, TOutput> convert,
        Action<TOutput> apply)
        where TSource : class =>
        sources.BindObservableValidationState(selectStates, convert, apply);

    /// <summary>Binds projected actual property states from the latest selected context.</summary>
    /// <typeparam name="TSource">The inferred selected source type.</typeparam>
    /// <param name="sources">The source and replacement stream.</param>
    /// <param name="selectContext">The typed context selector.</param>
    /// <param name="propertyPath">The complete property path.</param>
    /// <param name="convert">The actual matching-state projection.</param>
    /// <param name="apply">The output receiver.</param>
    /// <param name="strict">Whether only rules exclusively validating the path match.</param>
    /// <returns>The owned binding.</returns>
    [SuppressMessage("Design", "SST2309:Avoid optional public parameters", Justification = "Strict path selection is the fixed factory default, not a configurable default.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IValidationBinding FromPropertyStates<TSource>(
        IObservable<TSource?> sources,
        Func<TSource, IValidationContext?> selectContext,
        string propertyPath,
        Func<IList<IValidationState>, TOutput> convert,
        Action<TOutput> apply,
        bool strict = true)
        where TSource : class =>
        sources.BindObservablePropertyValidationState(selectContext, propertyPath, convert, apply, strict);
}
