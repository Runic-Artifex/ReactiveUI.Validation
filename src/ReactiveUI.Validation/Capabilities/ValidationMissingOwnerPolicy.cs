// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>The missing-parent delivery policy; null leaves remain actual values.</summary>
public enum ValidationMissingOwnerPolicy
{
    /// <summary>Delivers the type's default value.</summary>
    DefaultValue = 0,
    /// <summary>Suppresses delivery while a parent is missing.</summary>
    Suppress = 1,
    /// <summary>Delivers the explicitly supplied fallback.</summary>
    Fallback = 2,
}
