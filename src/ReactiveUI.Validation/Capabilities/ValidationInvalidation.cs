// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A notification requests a fresh typed read, including dependency-owner handoff.</summary>
[System.Diagnostics.DebuggerDisplay("ValidationInvalidation")]
public readonly struct ValidationInvalidation : IEquatable<ValidationInvalidation>
{
    /// <summary>Compares two payload-free invalidations.</summary>
    /// <param name="left">The first invalidation.</param>
    /// <param name="right">The second invalidation.</param>
    /// <returns>Always true.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ValidationInvalidation left, ValidationInvalidation right) => true;

    /// <summary>Compares two payload-free invalidations for inequality.</summary>
    /// <param name="left">The first invalidation.</param>
    /// <param name="right">The second invalidation.</param>
    /// <returns>Always false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ValidationInvalidation left, ValidationInvalidation right) => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ValidationInvalidation other) => true;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValidationInvalidation;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;
}
