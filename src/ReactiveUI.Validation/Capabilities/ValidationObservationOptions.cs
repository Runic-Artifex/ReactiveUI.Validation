// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Explicit value observation policies.</summary>
/// <typeparam name="TValue">The observed value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationObservationOptions")]
public sealed class ValidationObservationOptions<TValue>
{
    /// <summary>Initializes a new instance of the <see cref="ValidationObservationOptions{TValue}"/> class.</summary>
    /// <param name="missingOwner">The missing-parent policy.</param>
    /// <param name="fallback">Required for the fallback policy, otherwise unused.</param>
    /// <param name="comparer">Value equality; reference identity can be supplied for owner selections.</param>
    /// <param name="emitEveryInvalidation">Whether all invalidations deliver even equal values.</param>
    /// <exception cref="ArgumentException">Fallback policy has no callback.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The missing-owner policy is undefined.</exception>
    /// <exception cref="ArgumentException">Fallback mode has no fallback delegate.</exception>
    public ValidationObservationOptions(
        ValidationMissingOwnerPolicy missingOwner,
        Func<TValue>? fallback,
        IEqualityComparer<TValue> comparer,
        bool emitEveryInvalidation)
    {
        ArgumentExceptionHelper.ThrowIfNull(comparer);
        if (!Enum.IsDefined(missingOwner))
        {
            throw new ArgumentOutOfRangeException(nameof(missingOwner));
        }

        if (missingOwner == ValidationMissingOwnerPolicy.Fallback && fallback is null)
        {
            throw new ArgumentException("Fallback policy requires a typed fallback callback.", nameof(fallback));
        }

        MissingOwner = missingOwner;
        Fallback = fallback;
        Comparer = comparer;
        EmitEveryInvalidation = emitEveryInvalidation;
    }

    /// <summary>Gets the modern default options.</summary>
    public static ValidationObservationOptions<TValue> Default { get; } =
        new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<TValue>.Default, false);

    /// <summary>Gets the missing-parent policy.</summary>
    public ValidationMissingOwnerPolicy MissingOwner { get; }

    /// <summary>Gets the typed fallback callback.</summary>
    public Func<TValue>? Fallback { get; }

    /// <summary>Gets the value comparer.</summary>
    public IEqualityComparer<TValue> Comparer { get; }

    /// <summary>Gets whether equal-value invalidations deliver.</summary>
    public bool EmitEveryInvalidation { get; }
}
