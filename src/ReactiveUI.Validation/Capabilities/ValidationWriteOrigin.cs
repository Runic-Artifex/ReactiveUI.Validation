// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Identifies a validation writer belonging to one explicit storage policy.</summary>
[System.Diagnostics.DebuggerDisplay("ValidationWriteOrigin")]
public sealed class ValidationWriteOrigin
{
    /// <summary>Initializes a new instance of the <see cref="ValidationWriteOrigin"/> class.</summary>
    /// <param name="policy">The owning storage policy.</param>
    /// <param name="slot">The declared slot, or null for a whole-storage writer.</param>
    internal ValidationWriteOrigin(ValidationStoragePolicy policy, ValidationPath? slot)
    {
        Policy = policy;
        SlotIdentity = slot;
    }

    /// <summary>Gets the explicit structural slot, or null for whole-storage ownership.</summary>
    public ValidationPath? SlotIdentity { get; internal set; }

    /// <summary>Gets or sets whether the owning receipt was disposed.</summary>
    internal bool IsDisposed { get; set; }

    /// <summary>Gets or sets whether a coalesced write is pending.</summary>
    internal bool IsPending { get; set; }

    /// <summary>Gets or sets the external epoch admitted by pending work.</summary>
    internal long PendingExternalRevision { get; set; }

    /// <summary>Gets or sets the failure relay owned by the writer subscription.</summary>
    internal Action<Exception>? Failure { get; set; }

    /// <summary>Gets the policy that admits this origin as a peer writer.</summary>
    internal ValidationStoragePolicy Policy { get; }
}
