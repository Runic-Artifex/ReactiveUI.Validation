// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A current ownership selection, including absence that suppresses presentation without retaining an old subscription.</summary>
/// <typeparam name="TValue">The selected value type.</typeparam>
internal readonly record struct ValidationOwnerRead<TValue>
{
    /// <summary>Initializes a new instance of the <see cref="ValidationOwnerRead{TValue}"/> struct.</summary>
    /// <param name="hasOwner">Whether the read selected a real current owner.</param>
    /// <param name="value">The selected value; ownership selections use the type's default when their owner is absent.</param>
    /// <param name="suppressMissing">Whether an absent owner suppresses presentation values.</param>
    internal ValidationOwnerRead(bool hasOwner, TValue value, bool suppressMissing)
    {
        HasOwner = hasOwner;
        Value = value;
        SuppressMissing = suppressMissing;
    }

    /// <summary>Gets whether a real owner exists; a present default or null value still has an owner.</summary>
    internal bool HasOwner { get; }

    /// <summary>Gets the selected value; ownership roles exclude missing-owner fallback values.</summary>
    internal TValue Value { get; }

    /// <summary>Gets whether an absent selection suppresses presentation values.</summary>
    internal bool SuppressMissing { get; }
}
