// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Identifies output semantics shared by familiar extensions and static binding factories.</summary>
internal enum BindingProjectionKind
{
    /// <summary>Formats text and selects the first nonempty property message when appropriate.</summary>
    Text = 0,

    /// <summary>Passes full states to the caller's converter.</summary>
    ConvertState = 1,

    /// <summary>Passes original aggregate or property states directly to the callback.</summary>
    RawState = 2,

    /// <summary>Passes original property states and their ordered formatted outputs together.</summary>
    FormattedPropertyStates = 3,

    /// <summary>Passes the original helper state and its formatted output together.</summary>
    FormattedHelperState = 4,

    /// <summary>Passes the formatted aggregate model output.</summary>
    FormattedModelState = 5,
}
