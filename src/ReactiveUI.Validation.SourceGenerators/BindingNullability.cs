// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Checks the nullable contract of the actual target storage after normal conversion classification.</summary>
internal static class BindingNullability
{
    /// <summary>Detects nullable assignments that require an explicit caller-defined target policy.</summary>
    /// <param name="source">The original projected value contract.</param>
    /// <param name="target">The real writable storage contract.</param>
    /// <param name="conversion">The selected user-defined conversion, if any.</param>
    /// <returns>Whether nullable state could violate the target's declared contract.</returns>
    internal static bool RequiresPolicy(ITypeSymbol source, ITypeSymbol target, IMethodSymbol? conversion = null)
    {
        if (HasConversionRisk(source, target, conversion) || IsNarrowing(source, target))
        {
            return true;
        }

        return source is IArrayTypeSymbol sourceArray && target is IArrayTypeSymbol targetArray
            ? RequiresPolicy(sourceArray.ElementType, targetArray.ElementType)
            : source is INamedTypeSymbol namedSource && target is INamedTypeSymbol namedTarget
                && FindContract(namedSource, namedTarget) is { } contract && HasContractRisk(contract, namedTarget);
    }

    /// <summary>Checks nullable input and output slots of an explicitly selected conversion operator.</summary>
    /// <param name="source">The original projected input.</param>
    /// <param name="target">The actual writable storage.</param>
    /// <param name="conversion">The selected operator, if any.</param>
    /// <returns>Whether an operator input or result narrows nullable state.</returns>
    private static bool HasConversionRisk(ITypeSymbol source, ITypeSymbol target, IMethodSymbol? conversion) =>
        conversion is not null && (RequiresPolicy(conversion.ReturnType, target)
            || (conversion.Parameters.Length == 1 && RequiresPolicy(source, conversion.Parameters[0].Type)));

    /// <summary>Checks the immediate reference nullable annotation without assuming a type parameter is a value.</summary>
    /// <param name="source">The projected reference or open type.</param>
    /// <param name="target">The real writable slot.</param>
    /// <returns>Whether available null is prohibited by the target contract.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNarrowing(ITypeSymbol source, ITypeSymbol target) =>
        !source.IsValueType && !target.IsValueType && source.NullableAnnotation == NullableAnnotation.Annotated
            && target.NullableAnnotation == NullableAnnotation.NotAnnotated;

    /// <summary>Checks constructed containing types and variance-aware nested nullable slots.</summary>
    /// <param name="contract">The source's constructed target contract.</param>
    /// <param name="namedTarget">The actual writable named contract.</param>
    /// <returns>Whether an invariant or directional slot narrows nullable state.</returns>
    private static bool HasContractRisk(INamedTypeSymbol contract, INamedTypeSymbol namedTarget)
    {
        if (contract.ContainingType is { } sourceOwner && namedTarget.ContainingType is { } targetOwner
            && !SymbolEqualityComparer.IncludeNullability.Equals(sourceOwner, targetOwner))
        {
            return true;
        }

        for (var index = 0; index < contract.TypeArguments.Length; index++)
        {
            var sourceArgument = contract.TypeArguments[index];
            var targetArgument = namedTarget.TypeArguments[index];
            var variance = namedTarget.IsTupleType ? VarianceKind.Out : namedTarget.TypeParameters[index].Variance;
            var requiresPolicy = variance switch
            {
                VarianceKind.Out => RequiresPolicy(sourceArgument, targetArgument),
                VarianceKind.In => RequiresPolicy(targetArgument, sourceArgument),
                _ => !SymbolEqualityComparer.IncludeNullability.Equals(sourceArgument, targetArgument),
            };
            if (requiresPolicy)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the constructed source interface or base class corresponding to the target contract.</summary>
    /// <param name="source">The projected named value type.</param>
    /// <param name="target">The writable named storage type.</param>
    /// <returns>The source's exact constructed contract, or null when conversion uses another route.</returns>
    private static INamedTypeSymbol? FindContract(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        for (var current = source; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target.OriginalDefinition))
            {
                return current;
            }
        }

        foreach (var contract in source.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, target.OriginalDefinition))
            {
                return contract;
            }
        }

        return null;
    }
}
