// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
#else
using ReactiveUI;
#endif

namespace Runic.Validation.Examples;

/// <summary>A child with immutable identity and an observable editable name.</summary>
/// <param name="id">Immutable domain key.</param>
/// <param name="name">Initial name.</param>
internal sealed class RecipeChild(Guid id, string name) : ReactiveObject
{
    /// <summary>Gets or sets the name observed by AutoRefresh.</summary>
    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = name;

    /// <summary>Gets the immutable key used for replacement and removal.</summary>
    internal Guid Id { get; } = id;
}
