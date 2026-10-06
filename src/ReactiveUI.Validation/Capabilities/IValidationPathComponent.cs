// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A component exposing current structural metadata without losing legacy presentation names.</summary>
public interface IValidationPathComponent : IPropertyValidationComponent
{
    /// <summary>Gets the current value's complete path snapshot.</summary>
    IReadOnlyList<ValidationPath> ValidationPaths { get; }

    /// <summary>Gets path changes, including an initial snapshot when available.</summary>
    IObservable<IReadOnlyList<ValidationPath>> ValidationPathsChanged { get; }

    /// <summary>Matches exact structural identity; an explicitly legacy query or stored path instead uses its complete ordinal name.</summary>
    /// <param name="path">The queried identity.</param>
    /// <param name="exclusively">Whether this must be the rule's only identity.</param>
    /// <returns>Whether this rule matches.</returns>
    bool ContainsPath(ValidationPath path, bool exclusively);
}
