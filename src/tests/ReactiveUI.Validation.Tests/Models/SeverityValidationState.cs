// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests.Models;
#else
namespace ReactiveUI.Validation.Tests.Models;
#endif

/// <summary>A custom value-type state whose metadata can change while validity and text stay constant.</summary>
/// <param name="Severity">The presentation severity.</param>
internal readonly record struct SeverityValidationState(int Severity) : IValidationState
{
    /// <inheritdoc/>
    public bool IsValid => false;

    /// <inheritdoc/>
    public IValidationText Text => ValidationText.None;
}
