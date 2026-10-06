// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers legal assignments for anonymous and lexically private output types.</summary>
public static class ValidationTargetAccess
{
    /// <summary>Creates fixed-slot access without retaining the output inference witness.</summary>
    /// <typeparam name="TOwner">The stable reference owner.</typeparam>
    /// <typeparam name="TOut">The inferred output type.</typeparam>
    /// <param name="owner">The reference identity.</param>
    /// <param name="outputWitness">Supplies the output type without being retained.</param>
    /// <param name="assign">The legal typed assignment.</param>
    /// <returns>The assignment access.</returns>
    public static ValidationTargetAccess<TOut> Present<TOwner, TOut>(TOwner owner, TOut outputWitness, Action<TOut> assign)
        where TOwner : class
    {
        _ = outputWitness;
        return ValidationTargetAccess<TOut>.Present(owner, assign);
    }

    /// <summary>Creates current indexed-slot access without retaining the output inference witness.</summary>
    /// <typeparam name="TOwner">The stable reference owner.</typeparam>
    /// <typeparam name="TOut">The inferred output type.</typeparam>
    /// <param name="owner">The reference identity.</param>
    /// <param name="outputWitness">Supplies the output type without being retained.</param>
    /// <param name="slotIdentity">The current structural slot identity.</param>
    /// <param name="assign">The legal typed assignment.</param>
    /// <returns>The assignment access.</returns>
    public static ValidationTargetAccess<TOut> Present<TOwner, TOut>(TOwner owner, TOut outputWitness, ValidationPath slotIdentity, Action<TOut> assign)
        where TOwner : class
    {
        _ = outputWitness;
        return ValidationTargetAccess<TOut>.Present(owner, slotIdentity, assign);
    }
}
