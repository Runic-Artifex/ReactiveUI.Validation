// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Performs synchronous reads and validation without creating a subscription or retaining borrowed storage.</summary>
public static class ValidationSnapshot
{
    /// <summary>Reads an ordinary snapshot from the caller's current borrowed storage.</summary>
    /// <typeparam name="TSource">The source, which may be a ref struct.</typeparam>
    /// <typeparam name="TValue">The ordinary output value.</typeparam>
    /// <param name="source">The borrowed storage.</param>
    /// <param name="reader">The synchronous typed reader.</param>
    /// <returns>The selected output.</returns>
    /// <remarks>Reading a copied struct observes the copy. Long-lived observation requires explicitly owned reference storage.</remarks>
    [SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "A scoped readonly borrow is the explicit stack-only snapshot contract.")]
    public static TValue Read<TSource, TValue>(scoped in TSource source, ValidationSnapshotReader<TSource, TValue> reader)
        where TSource : allows ref struct
    {
        ArgumentExceptionHelper.ThrowIfNull(reader);
        return reader(in source);
    }

    /// <summary>Computes a complete validation state synchronously from borrowed storage.</summary>
    /// <typeparam name="TSource">The source, which may be a ref struct.</typeparam>
    /// <param name="source">The borrowed storage.</param>
    /// <param name="validate">The synchronous typed validator.</param>
    /// <returns>The complete state; a null result fails immediately.</returns>
    /// <exception cref="InvalidOperationException">The validator returns null.</exception>
    [SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "A scoped readonly borrow is the explicit stack-only validation contract.")]
    public static IValidationState Validate<TSource>(scoped in TSource source, ValidationSnapshotValidator<TSource> validate)
        where TSource : allows ref struct
    {
        ArgumentExceptionHelper.ThrowIfNull(validate);
        return validate(in source) ?? throw new InvalidOperationException("The snapshot validator returned null.");
    }
}
