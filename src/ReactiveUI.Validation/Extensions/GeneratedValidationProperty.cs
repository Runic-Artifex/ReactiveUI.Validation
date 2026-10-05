// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>A statically selected property notification in a generated observation chain.</summary>
/// <remarks>The owner getter must return the current owner, or null when an intermediate object is null.</remarks>
[System.Diagnostics.DebuggerDisplay("{PropertyName}")]
public sealed class GeneratedValidationProperty
{
    /// <summary>Initializes a new instance of the <see cref="GeneratedValidationProperty"/> class.</summary>
    /// <param name="owner">Gets the current notifying owner.</param>
    /// <param name="propertyName">The exact property notification name.</param>
    public GeneratedValidationProperty(Func<INotifyPropertyChanged?> owner, string propertyName)
    {
        ArgumentExceptionHelper.ThrowIfNull(owner);
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(propertyName);
        Owner = owner;
        PropertyName = propertyName;
    }

    /// <summary>Gets the current owner selector.</summary>
    internal Func<INotifyPropertyChanged?> Owner { get; }

    /// <summary>Gets the exact notification name.</summary>
    internal string PropertyName { get; }
}
