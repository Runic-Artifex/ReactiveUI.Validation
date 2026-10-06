// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>The closed operation and selector role requested by a normal API call.</summary>
[DebuggerDisplay("{Role}: {OperationIdentity}")]
public readonly struct ValidationPlanRequest : IEquatable<ValidationPlanRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ValidationPlanRequest"/> struct.</summary>
    /// <param name="role">The supplied selector's purpose.</param>
    /// <param name="operationIdentity">The stable operation identity agreed with the provider.</param>
    /// <exception cref="ArgumentOutOfRangeException">The role is not defined.</exception>
    public ValidationPlanRequest(ValidationPlanRole role, string operationIdentity)
    {
        ArgumentExceptionHelper.ThrowIfNull(operationIdentity);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Role = role;
        OperationIdentity = operationIdentity;
    }

    /// <summary>Gets the expression's purpose.</summary>
    public ValidationPlanRole Role { get; }

    /// <summary>Gets the stable operation identity, or null for a default request.</summary>
    public string? OperationIdentity { get; }

    /// <summary>Compares two requests by ordinal operation identity and role.</summary>
    /// <param name="left">The first request.</param>
    /// <param name="right">The second request.</param>
    /// <returns>Whether both requests have the same identity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ValidationPlanRequest left, ValidationPlanRequest right) => left.Equals(right);

    /// <summary>Compares two requests for different identities.</summary>
    /// <param name="left">The first request.</param>
    /// <param name="right">The second request.</param>
    /// <returns>Whether the requests differ.</returns>
    public static bool operator !=(ValidationPlanRequest left, ValidationPlanRequest right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ValidationPlanRequest other) => Role == other.Role && string.Equals(OperationIdentity, other.OperationIdentity, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValidationPlanRequest other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Role, OperationIdentity is null ? 0 : StringComparer.Ordinal.GetHashCode(OperationIdentity));

    /// <summary>Rejects default or invalid dispatch requests.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The request has an undefined role.</exception>
    internal void Validate()
    {
        ArgumentExceptionHelper.ThrowIfNull(OperationIdentity);
        if (!Enum.IsDefined(Role))
        {
            throw new ArgumentOutOfRangeException(nameof(Role));
        }
    }
}
