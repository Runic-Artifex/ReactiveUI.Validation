// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Recognizes deliberate finite typed runtime dispatch in a caller's lexical scope.</summary>
internal static class ValidationRuntimeDispatchPolicy
{
    /// <summary>Checks a source location for a real runtime dispatch opt-in and facade.</summary>
    /// <param name="model">The caller's semantic model.</param>
    /// <param name="position">The original caller position.</param>
    /// <returns>Whether the normal call intentionally uses registered runtime dispatch.</returns>
    internal static bool IsOptedIn(SemanticModel model, int position)
    {
        for (var symbol = model.GetEnclosingSymbol(position); symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (HasOptIn(model.Compilation, symbol))
            {
                return true;
            }
        }

        return HasOptIn(model.Compilation, model.Compilation.Assembly);
    }

    /// <summary>Checks the declaring runtime identity rather than an attribute's short name.</summary>
    /// <param name="compilation">The caller's compilation.</param>
    /// <param name="symbol">The scope whose attributes are inspected.</param>
    /// <returns>Whether a supported finite-runtime contract was selected.</returns>
    private static bool HasOptIn(Compilation compilation, ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } type
                || type.ContainingAssembly.Name is not ("ReactiveUI.Validation" or "ReactiveUI.Validation.Reactive"))
            {
                continue;
            }

            var root = type.ContainingAssembly.Name;
            if (type.ToDisplayString() == $"{root}.Capabilities.ValidationRuntimeDispatchAttribute"
                && HasFacade(compilation, root, type.ContainingAssembly))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Requires the matching real facade so an unsupported runtime cannot waive dispatch.</summary>
    /// <param name="compilation">The caller's compilation.</param>
    /// <param name="root">The runtime namespace.</param>
    /// <param name="assembly">The attribute's runtime assembly.</param>
    /// <returns>Whether the finite typed runtime facade belongs to the same assembly.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasFacade(Compilation compilation, string root, IAssemblySymbol assembly) =>
        compilation.GetTypeByMetadataName($"{root}.Capabilities.ValidationRuntime") is { } facade
        && SymbolEqualityComparer.Default.Equals(facade.ContainingAssembly, assembly);
}
