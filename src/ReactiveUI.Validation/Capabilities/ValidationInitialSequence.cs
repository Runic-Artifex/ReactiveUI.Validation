// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Presentation-only compatibility preludes; these never change domain rule validity.</summary>
[Flags]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1008:Enums should have zero value",
    Justification = "Actual explicitly names the default sequence containing only actual current validation states.")]
public enum ValidationInitialSequence
{
    /// <summary>Delivers actual states without synthetic values.</summary>
    Actual = 0,
    /// <summary>Delivers an empty presentation collection before actual states.</summary>
    LegacyEmpty = 1 << 0,
    /// <summary>Delivers a synthetic valid presentation seed before actual states.</summary>
    LegacyValid = 1 << 1,
}
