// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>The purpose of an expression within a validation operation.</summary>
public enum ValidationPlanRole
{
    /// <summary>A value selected for a validation rule.</summary>
    RuleValue = 0,
    /// <summary>A model selected by a view.</summary>
    Model = 1,
    /// <summary>A validation context selected by a source.</summary>
    Context = 2,
    /// <summary>A validation helper selected by a source.</summary>
    Helper = 3,
    /// <summary>A property selected for a validation binding.</summary>
    Property = 4,
    /// <summary>A writable presentation target.</summary>
    Target = 5,
    /// <summary>The implicit context supplied by the validatable model contract.</summary>
    DefaultContext = 6,
}
