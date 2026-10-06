// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Routes all eleven static factory overloads through the shared binding lowering.</summary>
internal static class StaticBindingEmitter
{
    /// <summary>Emits a recognized static factory using the same semantic plans as familiar extensions.</summary>
    /// <param name="site">The original semantic invocation.</param>
    /// <param name="source">The generated interception source.</param>
    /// <param name="diagnostic">An actionable capability diagnostic.</param>
    /// <returns>Whether the call belongs to the static binding factory family.</returns>
    internal static bool TryEmit(CallSite site, out string? source, out Diagnostic? diagnostic)
    {
        source = null;
        diagnostic = null;
        return site.Method.Name is "ForProperty" or "ForViewModel" or "ForValidationHelperProperty"
            && BindingEmitter.TryEmit(site, out source, out diagnostic);
    }
}
