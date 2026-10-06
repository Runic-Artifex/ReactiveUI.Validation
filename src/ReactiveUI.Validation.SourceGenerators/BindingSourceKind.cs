// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Identifies the current helper or context stream selected by a binding.</summary>
internal enum BindingSourceKind
{
    /// <summary>The current model's default validation context.</summary>
    Context = 0,

    /// <summary>A selected helper's complete validation states.</summary>
    Helper = 1,

    /// <summary>A selected context's complete validation states.</summary>
    SelectedContext = 2,
}
