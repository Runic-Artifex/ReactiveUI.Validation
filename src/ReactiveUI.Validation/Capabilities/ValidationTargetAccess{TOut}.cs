// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A typed assignment to a stable reference owner; default represents an unavailable target.</summary>
/// <typeparam name="TOut">The assigned value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationTargetAccess")]
public readonly struct ValidationTargetAccess<TOut> : IEquatable<ValidationTargetAccess<TOut>>
{
    /// <summary>The bound _assign contract.</summary>
    private readonly Action<TOut>? _assign;

    /// <summary>Initializes a new instance of the <see cref="ValidationTargetAccess{TOut}"/> struct.</summary>
    /// <param name="identity">The stable owner.</param>
    /// <param name="slotIdentity">The current structural slot.</param>
    /// <param name="assign">The current typed assignment.</param>
    /// <param name="isCurrent">The optional current-value check.</param>
    private ValidationTargetAccess(object identity, ValidationPath? slotIdentity, Action<TOut> assign, Func<TOut, bool>? isCurrent = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(identity);
        ArgumentExceptionHelper.ThrowIfNull(assign);
        Identity = identity;
        SlotIdentity = slotIdentity;
        _assign = assign;
        IsCurrent = isCurrent;
    }

    /// <summary>Gets the stable owner identity, or null when unavailable.</summary>
    public object? Identity { get; }

    /// <summary>Gets the structural slot identity within the owner, or null for a fixed slot.</summary>
    public ValidationPath? SlotIdentity { get; }

    /// <summary>Gets the optional typed predicate checking whether current storage already contains the cached output.</summary>
    public Func<TOut, bool>? IsCurrent { get; }

    /// <summary>Gets whether assignment is currently available.</summary>
    public bool IsAvailable => Identity is not null;

    /// <summary>Creates a typed assignment to a stable owner.</summary>
    /// <typeparam name="TOwner">The reference owner or stable storage identity.</typeparam>
    /// <param name="identity">The identity, compared by reference independently of overridden equality.</param>
    /// <param name="assign">Assigns through legal access, including any required outward write-back.</param>
    /// <returns>The available assignment.</returns>
    public static ValidationTargetAccess<TOut> Present<TOwner>(TOwner identity, Action<TOut> assign)
        where TOwner : class => new(identity, null, assign);

    /// <summary>Creates an assignment to a current indexed or keyed slot within a stable owner.</summary>
    /// <typeparam name="TOwner">The reference owner or stable storage identity.</typeparam>
    /// <param name="identity">The owner identity, compared by reference.</param>
    /// <param name="slotIdentity">The structural member, index, or lens slot identity, sampled once with the assignment.</param>
    /// <param name="assign">Assigns through legal access using the same sampled slot.</param>
    /// <returns>The available assignment.</returns>
    public static ValidationTargetAccess<TOut> Present<TOwner>(TOwner identity, ValidationPath slotIdentity, Action<TOut> assign)
        where TOwner : class
    {
        ArgumentExceptionHelper.ThrowIfNull(slotIdentity);
        return new(identity, slotIdentity, assign);
    }

    /// <summary>Creates an assignment that checks current storage after invalidations and reentrant writes.</summary>
    /// <typeparam name="TOwner">The stable reference owner.</typeparam>
    /// <param name="identity">The owner identity.</param>
    /// <param name="assign">Assigns through the current storage.</param>
    /// <param name="isCurrent">Reads current storage and compares it with the cached output.</param>
    /// <returns>The available assignment.</returns>
    public static ValidationTargetAccess<TOut> Present<TOwner>(TOwner identity, Action<TOut> assign, Func<TOut, bool> isCurrent)
        where TOwner : class
    {
        ArgumentExceptionHelper.ThrowIfNull(isCurrent);
        return new(identity, null, assign, isCurrent);
    }

    /// <summary>Creates a checked assignment to a current keyed slot within a stable owner.</summary>
    /// <typeparam name="TOwner">The stable reference owner.</typeparam>
    /// <param name="identity">The owner identity.</param>
    /// <param name="slotIdentity">The current structural slot identity.</param>
    /// <param name="assign">Assigns through current storage using the sampled slot.</param>
    /// <param name="isCurrent">Reads current storage at that slot and compares the cached output.</param>
    /// <returns>The available assignment.</returns>
    public static ValidationTargetAccess<TOut> Present<TOwner>(TOwner identity, ValidationPath slotIdentity, Action<TOut> assign, Func<TOut, bool> isCurrent)
        where TOwner : class
    {
        ArgumentExceptionHelper.ThrowIfNull(slotIdentity);
        ArgumentExceptionHelper.ThrowIfNull(isCurrent);
        return new(identity, slotIdentity, assign, isCurrent);
    }

    /// <summary>Creates an unavailable target.</summary>
    /// <returns>The missing target.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationTargetAccess<TOut> Missing() => default;

    /// <summary>Compares assignment owner and slot identity.</summary>
    /// <param name="left">The first access.</param>
    /// <param name="right">The second access.</param>
    /// <returns>Whether identities are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ValidationTargetAccess<TOut> left, ValidationTargetAccess<TOut> right) => left.Equals(right);

    /// <summary>Compares assignment identities for inequality.</summary>
    /// <param name="left">The first access.</param>
    /// <param name="right">The second access.</param>
    /// <returns>Whether identities differ.</returns>
    public static bool operator !=(ValidationTargetAccess<TOut> left, ValidationTargetAccess<TOut> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ValidationTargetAccess<TOut> other) => ReferenceEquals(Identity, other.Identity)
        && EqualityComparer<ValidationPath?>.Default.Equals(SlotIdentity, other.SlotIdentity);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValidationTargetAccess<TOut> access && Equals(access);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Identity is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Identity), SlotIdentity);

    /// <summary>Applies a value when this target is available.</summary>
    /// <param name="value">The typed output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Assign(TOut value) => _assign?.Invoke(value);
}
