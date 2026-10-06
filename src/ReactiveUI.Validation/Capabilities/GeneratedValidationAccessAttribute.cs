// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Explicitly selects an exact generated access bridge for an existing property, field or accessor.</summary>
/// <remarks>Bridge generation must prove the actual declaration. This never authorizes discovering backing storage.</remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
[System.Diagnostics.DebuggerDisplay("GeneratedValidationAccessAttribute")]
public sealed class GeneratedValidationAccessAttribute : Attribute;
