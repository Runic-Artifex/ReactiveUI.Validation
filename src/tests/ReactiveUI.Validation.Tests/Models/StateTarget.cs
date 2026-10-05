// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests.Models;
#else
namespace ReactiveUI.Validation.Tests.Models;
#endif

/// <summary>A native nested target with a typed property.</summary>
internal sealed class StateTarget
{
    /// <summary>Gets or sets a value indicating whether validation is valid.</summary>
    public bool IsValid { get; set; }
}
